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

console.log('========================================================');
console.log('  RUNNING COMPLETE VERIFICATION SUITE FOR DESKTOP PET');
console.log('========================================================\n');

// 1. Verify animation-config.json exists and is valid
const configPath = path.resolve(__dirname, '../animation-config.json');
assert(fs.existsSync(configPath), 'animation-config.json exists on disk');

const config = JSON.parse(fs.readFileSync(configPath, 'utf8'));
assert(config.defaultAnimation === 'idle', 'Requirement 1: Default animation is set to "idle"');
assert(config.animations.kick !== undefined, 'Requirement 2: "kick" animation exists');
assert(config.animations.kick.nextAnimation === 'idle', 'Requirement 9: "kick" smoothly transitions back to "idle"');

// 2. Verify Frame Boundaries and Frame Counts (Requirement 3 & 5)
console.log('\n--- Checking Frame Boundaries & Verification ---');
assert(config.animations.idle.totalFrames === 35, 'idle has 35 frames (not assumed 36; empty cell discarded)');
assert(config.animations.kick.totalFrames === 31, 'kick has 31 frames (not assumed 36; 5 empty cells discarded)');
assert(config.animations.punch.totalFrames === 36, 'punch has 36 frames');
assert(config.animations.run.totalFrames === 16, 'run has 16 frames (not assumed 18; 2 empty cells discarded)');

// 3. Verify Extracted Frames preserve 32-bit RGBA Transparency (Requirement 4 & 10)
console.log('\n--- Checking Transparency Preservation ---');
const idleFrame0Path = path.resolve(__dirname, '../extracted_frames/idle/frame_000.png');
const kickFrame0Path = path.resolve(__dirname, '../extracted_frames/kick/frame_000.png');

assert(fs.existsSync(idleFrame0Path), 'Extracted idle frame 0 exists');
assert(fs.existsSync(kickFrame0Path), 'Extracted kick frame 0 exists');

const idleFrame0 = decodePNG(idleFrame0Path);
const kickFrame0 = decodePNG(kickFrame0Path);

assert(idleFrame0.width === 512 && idleFrame0.height === 384, 'Frame width is 512 and height is 384');

// Check corner pixel (should be fully transparent, alpha = 0, no black background)
const cornerAlphaIdle = idleFrame0.pixels[3];
const cornerAlphaKick = kickFrame0.pixels[3];
assert(cornerAlphaIdle === 0, 'Idle frame corner pixel is 100% transparent (alpha == 0, no black rectangle)');
assert(cornerAlphaKick === 0, 'Kick frame corner pixel is 100% transparent (alpha == 0, no black rectangle)');

// Check robot body pixel (e.g. at center 220, 200)
const robotAlphaIdle = idleFrame0.pixels[(200 * 512 + 220) * 4 + 3];
assert(robotAlphaIdle > 200, `Idle robot body pixel at (220, 200) has solid alpha: ${robotAlphaIdle}`);

// 4. Verify Consistent Size and Alignment (Requirement 8)
console.log('\n--- Checking Size & Position Consistency ---');
assert(config.globalAnchor.x === 220 && config.globalAnchor.y === 320, 'Global anchor is (220, 320) ground baseline');

for (const animName of ['idle', 'kick', 'punch', 'run']) {
  const anim = config.animations[animName];
  const baselines = anim.frames.map(f => f.groundBaseline);
  const maxBaseline = Math.max(...baselines);
  assert(maxBaseline >= 320 && maxBaseline <= 324, `${animName} grounded baseline is aligned at y=${maxBaseline} (expected ~320-324)`);
}

// 5. Verify Animation Controller & State Transition (Requirements 6, 7, 9)
console.log('\n--- Checking Animation Controller Functionality ---');
const controller = new SpriteAnimationController(config);

assert(controller.getAnimationNames().includes('idle'), 'Controller registers idle');
assert(controller.getAnimationNames().includes('kick'), 'Controller registers kick');

// Test configurable FPS, loop, sequence
controller.play('idle', { fps: 30, loop: true });
assert(controller.currentAnimation.fps === 30, 'Configurable FPS works (set to 30)');
assert(controller.currentAnimation.loop === true, 'Configurable loop works (set to true)');

// Test custom frame sequence
const customSeq = [0, 1, 2, 3, 2, 1, 0];
controller.setFrameSequence(customSeq);
assert(JSON.stringify(controller.currentAnimation.frameSequence) === JSON.stringify(customSeq), 'Custom frame sequence supported');

// Test dynamic addition of new animations
controller.registerAnimation('celebrate', {
  fps: 18,
  loop: false,
  nextAnimation: 'idle',
  totalFrames: 10,
  frameSequence: [0, 1, 2, 3, 4, 5, 6, 7, 8, 9]
});
assert(controller.getAnimationNames().includes('celebrate'), 'Dynamic registration of new animations works');

// Test kick transition to idle
let transitionedToIdle = false;
controller.on('transition', e => {
  if (e.from === 'kick' && e.to === 'idle') {
    transitionedToIdle = true;
  }
});

// Advance through kick animation
controller.play('kick', { fps: 60 });
while (controller.currentAnimation.name === 'kick') {
  controller.advanceFrame();
}

assert(transitionedToIdle, 'Requirement 9: Smooth transition from kick back to idle verified');
assert(controller.currentAnimation.name === 'idle', 'Controller successfully landed back in idle state');

console.log('\n========================================================');
console.log('  🎉 ALL 10 REQUIREMENTS SUCCESSFULLY VERIFIED!');
console.log('========================================================\n');
