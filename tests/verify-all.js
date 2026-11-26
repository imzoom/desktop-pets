const fs = require('fs');
const path = require('path');
const { SpriteAnimationController } = require('../src/animation-controller');
const { decodePNG } = require('../src/extractor');

function assert(cond, msg) {
  if (!cond) {
    console.error(`❌ FAILED: ${msg}`);
    process.exit(1);
  } else {
    console.log(`✅ PASSED: ${msg}`);
  }
}

console.log('================================================================');
console.log('  RUNNING VERIFICATION SUITE: MULTI-CHARACTER & SWORD POWER');
console.log('================================================================\n');

// 1. Verify animation-config.json exists and supports both characters
const configPath = path.resolve(__dirname, '../animation-config.json');
assert(fs.existsSync(configPath), 'animation-config.json exists on disk');

const config = JSON.parse(fs.readFileSync(configPath, 'utf8'));
assert(config.characters !== undefined, 'Multi-character architecture present');
assert(config.characters['hooded-blade'] !== undefined, 'Hooded Blade character exists');
assert(config.characters['robot'] !== undefined, 'Robot character exists');

// 2. Verify Hooded Blade animations & Sword Power
console.log('\n--- Checking Hooded Blade & Sword Power ---');
const blade = config.characters['hooded-blade'];
assert(blade.animations.idle.totalFrames === 32, 'Hooded Blade idle has 32 frames');
assert(blade.animations.run.totalFrames === 24, 'Hooded Blade run has 24 frames');
assert(blade.animations.attack.totalFrames === 32, 'Hooded Blade attack has 32 frames');
assert(blade.animations['sword-power'] !== undefined, 'Sword Power animation exists');
assert(blade.animations['sword-power'].totalFrames === 32, 'Sword Power has 32 frames');
assert(blade.animations['sword-power'].nextAnimation === 'idle', 'Sword Power smoothly transitions to idle');
assert(blade.animations.jump.totalFrames === 7, 'Hooded Blade jump has 7 frames');

// 3. Verify Extracted Frames preserve 32-bit RGBA Transparency & Sword Power FX
console.log('\n--- Checking Sword Power Frames & Transparency ---');
const swordPowerFrame16Path = path.resolve(__dirname, '../extracted_frames/hooded-blade/sword-power/frame_016.png');
assert(fs.existsSync(swordPowerFrame16Path), 'Sword Power frame 16 exists');

const frame16 = decodePNG(swordPowerFrame16Path);
assert(frame16.width === 512 && frame16.height === 512, 'Hooded blade frame is 512x512');

// Verify corner pixel is transparent
assert(frame16.pixels[3] === 0, 'Frame corner is 100% transparent (no black rectangle)');

// Verify glowing energy crescent pixels exist in frame 16 (during the slash strike)
let energyPixels = 0;
for (let i = 0; i < frame16.pixels.length; i += 4) {
  const r = frame16.pixels[i];
  const g = frame16.pixels[i+1];
  const b = frame16.pixels[i+2];
  const a = frame16.pixels[i+3];
  if (a > 100 && g > 180 && (g > r || b > 100)) {
    energyPixels++;
  }
}
assert(energyPixels > 1000, `Sword Power frame 16 contains radiant emerald/cyan energy blade particles (${energyPixels} pixels)`);

// 4. Verify Animation Controller & Smooth Transitions
console.log('\n--- Checking Controller State Transitions ---');
const controller = new SpriteAnimationController(blade);
assert(controller.getAnimationNames().includes('sword-power'), 'Controller loaded Sword Power');

let transitionedBackToIdle = false;
controller.on('transition', e => {
  if (e.from === 'sword-power' && e.to === 'idle') {
    transitionedBackToIdle = true;
  }
});

controller.play('sword-power', { fps: 60 });
while (controller.currentAnimation.name === 'sword-power') {
  controller.advanceFrame();
}
assert(transitionedBackToIdle, 'Sword Power smoothly transitions back to idle');
assert(controller.currentAnimation.name === 'idle', 'Controller landed in idle state');

// 5. Verify Robot Character Backwards Compatibility
console.log('\n--- Checking Robot Character Compatibility ---');
const robot = config.characters['robot'];
assert(robot.animations.idle.totalFrames === 35, 'Robot idle has 35 frames');
assert(robot.animations.kick.totalFrames === 31, 'Robot kick has 31 frames');
assert(robot.animations.punch.totalFrames === 36, 'Robot punch has 36 frames');
assert(robot.animations.run.totalFrames === 16, 'Robot run has 16 frames');

console.log('\n================================================================');
console.log('  🎉 ALL VERIFICATION TESTS PASSED SUCCESSFULLY!');
console.log('================================================================\n');
