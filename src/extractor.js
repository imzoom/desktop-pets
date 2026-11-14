const fs = require('fs');
const path = require('path');
const zlib = require('zlib');

// CRC32 table for PNG generation
const crcTable = new Uint32Array(256);
for (let n = 0; n < 256; n++) {
  let c = n;
  for (let k = 0; k < 8; k++) {
    c = (c & 1) ? (0xedb88320 ^ (c >>> 1)) : (c >>> 1);
  }
  crcTable[n] = c;
}

function crc32(buf) {
  let crc = 0xffffffff;
  for (let i = 0; i < buf.length; i++) {
    crc = crcTable[(crc ^ buf[i]) & 0xff] ^ (crc >>> 8);
  }
  return (crc ^ 0xffffffff) >>> 0;
}

function createChunk(type, data) {
  const len = data.length;
  const chunk = Buffer.alloc(12 + len);
  chunk.writeUInt32BE(len, 0);
  chunk.write(type, 4, 4, 'ascii');
  data.copy(chunk, 8);
  const typeAndData = chunk.subarray(4, 8 + len);
  chunk.writeUInt32BE(crc32(typeAndData), 8 + len);
  return chunk;
}

function decodePNG(filePath) {
  const buf = fs.readFileSync(filePath);
  let pos = 8, width, height, bitDepth, colorType;
  const idatChunks = [];
  while (pos < buf.length) {
    const len = buf.readUInt32BE(pos);
    const type = buf.toString('ascii', pos + 4, pos + 8);
    if (type === 'IHDR') {
      width = buf.readUInt32BE(pos + 8);
      height = buf.readUInt32BE(pos + 12);
      bitDepth = buf.readUInt8(pos + 16);
      colorType = buf.readUInt8(pos + 17);
    } else if (type === 'IDAT') {
      idatChunks.push(buf.subarray(pos + 8, pos + 8 + len));
    }
    pos += 12 + len;
  }
  const decompressed = zlib.inflateSync(Buffer.concat(idatChunks));
  const bytesPerPixel = 4;
  const scanlineLength = width * bytesPerPixel;
  const pixels = Buffer.alloc(width * height * 4);
  let srcPos = 0, dstPos = 0;
  function paeth(a, b, c) {
    const p = a + b - c;
    const pa = Math.abs(p - a), pb = Math.abs(p - b), pc = Math.abs(p - c);
    return (pa <= pb && pa <= pc) ? a : (pb <= pc) ? b : c;
  }
  for (let y = 0; y < height; y++) {
    const filterType = decompressed[srcPos++];
    const lineStart = dstPos;
    const prevLineStart = (y === 0) ? -1 : (y - 1) * scanlineLength;
    for (let x = 0; x < scanlineLength; x++) {
      const raw = decompressed[srcPos++];
      const a = (x >= bytesPerPixel) ? pixels[lineStart + x - bytesPerPixel] : 0;
      const b = (prevLineStart >= 0) ? pixels[prevLineStart + x] : 0;
      const c = (x >= bytesPerPixel && prevLineStart >= 0) ? pixels[prevLineStart + x - bytesPerPixel] : 0;
      let val = 0;
      switch (filterType) {
        case 0: val = raw; break;
        case 1: val = (raw + a) & 0xff; break;
        case 2: val = (raw + b) & 0xff; break;
        case 3: val = (raw + Math.floor((a + b) / 2)) & 0xff; break;
        case 4: val = (raw + paeth(a, b, c)) & 0xff; break;
      }
      pixels[dstPos++] = val;
    }
  }
  return { width, height, pixels };
}

function encodePNG(width, height, rgbaBuffer) {
  const header = Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]);
  const ihdr = Buffer.alloc(13);
  ihdr.writeUInt32BE(width, 0);
  ihdr.writeUInt32BE(height, 4);
  ihdr[8] = 8;
  ihdr[9] = 6; // RGBA
  ihdr[10] = 0;
  ihdr[11] = 0;
  ihdr[12] = 0;
  const ihdrChunk = createChunk('IHDR', ihdr);

  const scanlineLength = width * 4;
  const rawData = Buffer.alloc(height * (1 + scanlineLength));
  for (let y = 0; y < height; y++) {
    rawData[y * (1 + scanlineLength)] = 0; // Filter: None
    rgbaBuffer.copy(rawData, y * (1 + scanlineLength) + 1, y * scanlineLength, (y + 1) * scanlineLength);
  }

  const compressed = zlib.deflateSync(rawData);
  const idatChunk = createChunk('IDAT', compressed);
  const iendChunk = createChunk('IEND', Buffer.alloc(0));

  return Buffer.concat([header, ihdrChunk, idatChunk, iendChunk]);
}

/**
 * Inspects a spritesheet and extracts non-empty, aligned frames.
 */
function extractSpritesheet(spritesheetPath, outputDir, animConfig) {
  console.log(`\nProcessing: ${path.basename(spritesheetPath)}...`);
  const img = decodePNG(spritesheetPath);
  const cellW = animConfig.cellWidth || 512;
  const cellH = animConfig.cellHeight || 384;
  const cols = Math.floor(img.width / cellW);
  const rows = Math.floor(img.height / cellH);

  if (!fs.existsSync(outputDir)) {
    fs.mkdirSync(outputDir, { recursive: true });
  }

  const framesMeta = [];
  let frameIndex = 0;

  for (let r = 0; r < rows; r++) {
    for (let c = 0; c < cols; c++) {
      // Analyze this cell
      let minX = cellW, maxX = -1, minY = cellH, maxY = -1, count = 0;
      let sumX = 0, sumY = 0;
      const cellPixels = Buffer.alloc(cellW * cellH * 4);

      for (let y = 0; y < cellH; y++) {
        for (let x = 0; x < cellW; x++) {
          const gx = c * cellW + x;
          const gy = r * cellH + y;
          const srcIdx = (gy * img.width + gx) * 4;
          const dstIdx = (y * cellW + x) * 4;

          const red = img.pixels[srcIdx];
          const green = img.pixels[srcIdx + 1];
          const blue = img.pixels[srcIdx + 2];
          const alpha = img.pixels[srcIdx + 3];

          cellPixels[dstIdx] = red;
          cellPixels[dstIdx + 1] = green;
          cellPixels[dstIdx + 2] = blue;
          cellPixels[dstIdx + 3] = alpha;

          if (alpha > 10) {
            count++;
            sumX += x;
            sumY += y;
            if (x < minX) minX = x;
            if (x > maxX) maxX = x;
            if (y < minY) minY = y;
            if (y > maxY) maxY = y;
          }
        }
      }

      // If cell has meaningful content (> 100 non-transparent pixels)
      if (count > 100) {
        const frameFileName = `frame_${String(frameIndex).padStart(3, '0')}.png`;
        const frameFilePath = path.join(outputDir, frameFileName);
        const pngBuf = encodePNG(cellW, cellH, cellPixels);
        fs.writeFileSync(frameFilePath, pngBuf);

        framesMeta.push({
          index: frameIndex,
          file: frameFileName,
          row: r,
          col: c,
          gridSource: { x: c * cellW, y: r * cellH, width: cellW, height: cellH },
          contentBox: {
            minX, maxX, minY, maxY,
            width: maxX - minX + 1,
            height: maxY - minY + 1
          },
          center: { x: +(sumX / count).toFixed(1), y: +(sumY / count).toFixed(1) },
          groundBaseline: maxY,
          pixelCount: count
        });

        frameIndex++;
      } else {
        console.log(`  Skipping empty padding cell at row ${r}, col ${c}`);
      }
    }
  }

  console.log(`  Extracted ${framesMeta.length} individual transparent frames to ${outputDir}`);
  return framesMeta;
}

function runExtraction() {
  const baseDir = path.resolve(__dirname, '..');
  const imagesDir = path.join(baseDir, 'images');
  const outputBase = path.join(baseDir, 'extracted_frames');

  // Animation specifications
  const animationsDef = {
    idle: {
      name: 'idle',
      sourceFile: path.join(imagesDir, 'idle-spritesheet.png'),
      cellWidth: 512,
      cellHeight: 384,
      fps: 24,
      loop: true,
      nextAnimation: null,
      description: 'Default idle robot breathing and stance animation'
    },
    kick: {
      name: 'kick',
      sourceFile: path.join(imagesDir, 'kick-spritesheet.png'),
      cellWidth: 512,
      cellHeight: 384,
      fps: 24,
      loop: false,
      nextAnimation: 'idle',
      description: 'Dynamic martial arts kick action, smoothly transitioning back to idle'
    },
    punch: {
      name: 'punch',
      sourceFile: path.join(imagesDir, 'punch-spritesheet.png'),
      cellWidth: 512,
      cellHeight: 384,
      fps: 24,
      loop: false,
      nextAnimation: 'idle',
      description: 'Forward punch strike action, smoothly transitioning back to idle'
    },
    run: {
      name: 'run',
      sourceFile: path.join(imagesDir, 'run-spritesheet.png'),
      cellWidth: 512,
      cellHeight: 384,
      fps: 24,
      loop: true,
      nextAnimation: null,
      description: 'Forward running sprint cycle'
    }
  };

  const fullConfig = {
    version: '1.0.0',
    character: 'RobotPet',
    globalAnchor: {
      // The shared anchor/pivot point across all animations
      // Anchor (220, 320) aligns the ground contact floor and torso center
      x: 220,
      y: 320
    },
    frameDimensions: {
      width: 512,
      height: 384
    },
    defaultAnimation: 'idle',
    animations: {}
  };

  for (const [key, anim] of Object.entries(animationsDef)) {
    if (!fs.existsSync(anim.sourceFile)) {
      console.warn(`Source file not found: ${anim.sourceFile}, skipping.`);
      continue;
    }
    const animOutputDir = path.join(outputBase, key);
    const frames = extractSpritesheet(anim.sourceFile, animOutputDir, anim);

    fullConfig.animations[key] = {
      name: anim.name,
      description: anim.description,
      fps: anim.fps,
      durationMs: Math.round((frames.length / anim.fps) * 1000),
      loop: anim.loop,
      nextAnimation: anim.nextAnimation,
      totalFrames: frames.length,
      frameSequence: frames.map(f => f.index),
      framesDir: `extracted_frames/${key}`,
      frames: frames
    };
  }

  const configPath = path.join(baseDir, 'animation-config.json');
  fs.writeFileSync(configPath, JSON.stringify(fullConfig, null, 2));
  console.log(`\nSuccessfully generated animation configuration: ${configPath}`);
  return fullConfig;
}

if (require.main === module) {
  runExtraction();
}

module.exports = {
  decodePNG,
  encodePNG,
  extractSpritesheet,
  runExtraction
};
