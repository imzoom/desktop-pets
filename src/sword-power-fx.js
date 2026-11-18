/**
 * Sword Power FX Generator
 * Synthesizes a high-energy anime/game-style Sword Power animation
 * by applying an emerald/cyan radiant energy aura, glowing shockwave crescent,
 * and particle projectile effects onto the character's attack frames.
 */

function blendAdditive(buffer, width, height, x, y, r, g, b, alpha) {
  const ix = Math.floor(x);
  const iy = Math.floor(y);
  if (ix < 0 || ix >= width || iy < 0 || iy >= height || alpha <= 0) return;

  const idx = (iy * width + ix) * 4;
  const factor = alpha / 255;

  buffer[idx] = Math.min(255, buffer[idx] + Math.floor(r * factor));
  buffer[idx + 1] = Math.min(255, buffer[idx + 1] + Math.floor(g * factor));
  buffer[idx + 2] = Math.min(255, buffer[idx + 2] + Math.floor(b * factor));
  buffer[idx + 3] = Math.max(buffer[idx + 3], alpha);
}

function drawGlowDot(buffer, width, height, cx, cy, radius, r, g, b, intensity = 1.0) {
  const r2 = radius * radius;
  const minX = Math.max(0, Math.floor(cx - radius));
  const maxX = Math.min(width - 1, Math.ceil(cx + radius));
  const minY = Math.max(0, Math.floor(cy - radius));
  const maxY = Math.min(height - 1, Math.ceil(cy + radius));

  for (let y = minY; y <= maxY; y++) {
    for (let x = minX; x <= maxX; x++) {
      const dx = x - cx;
      const dy = y - cy;
      const d2 = dx * dx + dy * dy;
      if (d2 <= r2) {
        const dist = Math.sqrt(d2);
        const falloff = Math.pow(1 - dist / radius, 1.8);
        const alpha = Math.min(255, Math.floor(255 * falloff * intensity));
        blendAdditive(buffer, width, height, x, y, r, g, b, alpha);
      }
    }
  }
}

function drawEnergyArc(buffer, width, height, cx, cy, arcRadius, startAngle, endAngle, thickness, r, g, b, glowR, glowG, glowB) {
  const steps = 70;
  for (let s = 0; s <= steps; s++) {
    const t = s / steps;
    const angle = startAngle + (endAngle - startAngle) * t;
    // Taper thickness towards ends
    const taper = Math.sin(t * Math.PI);
    const effThick = thickness * taper;

    const px = cx + Math.cos(angle) * arcRadius;
    const py = cy + Math.sin(angle) * arcRadius;

    // Draw outer radiant glow
    drawGlowDot(buffer, width, height, px, py, effThick * 2.8, glowR, glowG, glowB, 0.55);
    // Draw bright energy core
    drawGlowDot(buffer, width, height, px, py, effThick, r, g, b, 0.95);
    // Draw white-hot center
    drawGlowDot(buffer, width, height, px, py, Math.max(1, effThick * 0.4), 255, 255, 255, 1.0);
  }
}

/**
 * Applies the Sword Power FX to a single frame.
 * @param {Buffer} frameBuffer 512x512 RGBA
 * @param {number} frameIndex 0..31
 * @returns {Buffer} Modified RGBA buffer
 */
function applySwordPowerFx(frameBuffer, frameIndex, width = 512, height = 512) {
  const output = Buffer.from(frameBuffer);

  // Phase 1: Charging Energy Aura (Frames 4 to 13)
  if (frameIndex >= 4 && frameIndex <= 13) {
    const chargeProgress = (frameIndex - 4) / 9; // 0 to 1
    const bladeTipX = 180 + chargeProgress * 40;
    const bladeTipY = 140 - chargeProgress * 30;

    // Pulsing charging orbs & sparkles
    const sparkCount = Math.floor(chargeProgress * 12) + 4;
    for (let i = 0; i < sparkCount; i++) {
      const angle = (i / sparkCount) * Math.PI * 2 + frameIndex * 0.4;
      const dist = (1 - chargeProgress * 0.7) * (35 + (i % 3) * 15);
      const sx = bladeTipX + Math.cos(angle) * dist;
      const sy = bladeTipY + Math.sin(angle) * dist;
      drawGlowDot(output, width, height, sx, sy, 4 + (i % 3), 100, 255, 200, 0.85);
    }

    // Glowing aura at blade
    drawGlowDot(output, width, height, bladeTipX, bladeTipY, 25 + chargeProgress * 35, 16, 230, 140, 0.6 * chargeProgress);
    drawGlowDot(output, width, height, bladeTipX, bladeTipY, 12, 180, 255, 220, 0.8 * chargeProgress);
  }

  // Phase 2: Unleashed Sword Power Crescent Slash (Frames 14 to 24)
  if (frameIndex >= 14 && frameIndex <= 24) {
    const slashStep = frameIndex - 14; // 0 to 10
    const progress = slashStep / 10;

    // The energy crescent moves forward from x=310 to x=495
    const waveCenterX = 260 + progress * 175;
    const waveCenterY = 220 - progress * 15;
    const waveRadius = 75 + progress * 40;

    const startAngle = -Math.PI * 0.42;
    const endAngle = Math.PI * 0.42;
    const thickness = Math.max(3, 16 * (1 - progress * 0.5));

    // Draw the massive energy crescent shockwave!
    drawEnergyArc(
      output, width, height,
      waveCenterX, waveCenterY,
      waveRadius,
      startAngle, endAngle,
      thickness,
      120, 255, 220, // Cyan-white core
      0, 255, 136    // Neon emerald outer aura
    );

    // Speed lines & sparks shooting forward
    for (let p = 0; p < 8; p++) {
      const sparkDist = (p + 1) * 8 * progress;
      const sparkY = waveCenterY + (p - 4) * 16;
      const sparkX = waveCenterX + waveRadius + sparkDist;
      drawGlowDot(output, width, height, sparkX, sparkY, 3 + (p % 4), 160, 255, 240, 0.9 * (1 - progress * 0.6));
    }
  }

  // Phase 3: Energy Dissipation & Blade Afterglow (Frames 22 to 30)
  if (frameIndex >= 22 && frameIndex <= 30) {
    const fadeProgress = (frameIndex - 22) / 8; // 0 to 1
    const fade = 1 - fadeProgress;

    // Lingering sparks
    for (let k = 0; k < 6; k++) {
      const px = 400 + k * 14 + Math.sin(frameIndex + k) * 10;
      const py = 180 + (k - 3) * 20 + Math.cos(frameIndex + k) * 10;
      drawGlowDot(output, width, height, px, py, 4 * fade, 60, 255, 180, 0.7 * fade);
    }
  }

  return output;
}

module.exports = { applySwordPowerFx };
