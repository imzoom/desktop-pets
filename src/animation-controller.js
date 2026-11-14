/**
 * Reusable Sprite Animation Controller
 * 
 * Supports:
 * - Configurable FPS, duration, loop, and custom frame sequences
 * - State transitions (e.g. Action -> Idle)
 * - Size and position consistency via anchor/pivot normalization
 * - Frame-by-frame rendering with transparency preservation
 * - Dynamic addition of new animations
 * - Callbacks for frame changes and animation completion
 */
class SpriteAnimationController {
  constructor(config = {}) {
    this.config = null;
    this.animations = new Map();
    this.currentAnimation = null;
    this.currentFrameIndex = 0;
    this.sequenceIndex = 0;
    this.isPlaying = false;
    this.playbackRate = 1.0;
    this.timerId = null;
    this.lastFrameTime = 0;
    this.globalAnchor = { x: 220, y: 320 };
    this.frameDimensions = { width: 512, height: 384 };
    this.defaultAnimation = 'idle';

    // Event listeners
    this.listeners = {
      frameChange: [],
      animationStart: [],
      animationComplete: [],
      transition: []
    };

    if (config) {
      this.loadConfig(config);
    }
  }

  /**
   * Load entire animation configuration object.
   */
  loadConfig(config) {
    this.config = config;
    if (config.globalAnchor) {
      this.globalAnchor = { ...config.globalAnchor };
    }
    if (config.frameDimensions) {
      this.frameDimensions = { ...config.frameDimensions };
    }
    if (config.defaultAnimation) {
      this.defaultAnimation = config.defaultAnimation;
    }
    if (config.animations) {
      for (const [key, anim] of Object.entries(config.animations)) {
        this.registerAnimation(key, anim);
      }
    }
  }

  /**
   * Register or override an animation in the controller.
   * Allows new animations to easily be added later.
   */
  registerAnimation(name, animDef) {
    const totalFrames = animDef.totalFrames || (animDef.frames ? animDef.frames.length : 0);
    const fps = animDef.fps || 24;
    const defaultSeq = Array.from({ length: totalFrames }, (_, i) => i);
    const frameSequence = animDef.frameSequence && animDef.frameSequence.length > 0
      ? [...animDef.frameSequence]
      : defaultSeq;

    const normalized = {
      name,
      description: animDef.description || '',
      fps,
      loop: animDef.loop !== undefined ? animDef.loop : true,
      nextAnimation: animDef.nextAnimation || null,
      frameSequence,
      totalFrames,
      durationMs: animDef.durationMs || Math.round((frameSequence.length / fps) * 1000),
      framesDir: animDef.framesDir || `extracted_frames/${name}`,
      frames: animDef.frames || [],
      anchor: animDef.anchor || this.globalAnchor,
      onComplete: animDef.onComplete || null
    };

    this.animations.set(name, normalized);
  }

  /**
   * Get an animation definition by name.
   */
  getAnimation(name) {
    return this.animations.get(name) || null;
  }

  /**
   * List all registered animation names.
   */
  getAnimationNames() {
    return Array.from(this.animations.keys());
  }

  /**
   * Play an animation by name.
   * Supports runtime options overrides (fps, loop, frameSequence, onComplete).
   */
  play(name, options = {}) {
    const anim = this.getAnimation(name);
    if (!anim) {
      console.warn(`Animation "${name}" not found.`);
      return false;
    }

    const previousAnim = this.currentAnimation ? this.currentAnimation.name : null;

    // Apply runtime overrides if provided
    this.currentAnimation = {
      ...anim,
      fps: options.fps !== undefined ? options.fps : anim.fps,
      loop: options.loop !== undefined ? options.loop : anim.loop,
      frameSequence: options.frameSequence ? [...options.frameSequence] : [...anim.frameSequence],
      nextAnimation: options.nextAnimation !== undefined ? options.nextAnimation : anim.nextAnimation,
      onComplete: options.onComplete || anim.onComplete
    };

    this.sequenceIndex = 0;
    this.currentFrameIndex = this.currentAnimation.frameSequence[0] || 0;
    this.isPlaying = true;
    this.lastFrameTime = performance.now();

    this.emit('animationStart', {
      animation: this.currentAnimation.name,
      previousAnimation: previousAnim
    });

    this.notifyFrameChange();
    this.scheduleNextFrame();
    return true;
  }

  /**
   * Smoothly transitions from the current animation to another.
   */
  transitionTo(nextAnimName, options = {}) {
    if (this.currentAnimation && this.currentAnimation.name === nextAnimName) {
      return; // Already playing this animation
    }
    const from = this.currentAnimation ? this.currentAnimation.name : null;
    this.emit('transition', { from, to: nextAnimName });
    this.play(nextAnimName, options);
  }

  /**
   * Schedule the next frame using precise delta timing.
   */
  scheduleNextFrame() {
    if (!this.isPlaying || !this.currentAnimation) return;

    if (this.timerId) {
      clearTimeout(this.timerId);
      this.timerId = null;
    }

    const effectiveFps = (this.currentAnimation.fps || 24) * this.playbackRate;
    const intervalMs = 1000 / Math.max(1, effectiveFps);

    this.timerId = setTimeout(() => {
      this.advanceFrame();
    }, intervalMs);
  }

  /**
   * Advance to the next frame in sequence.
   */
  advanceFrame() {
    if (!this.isPlaying || !this.currentAnimation) return;

    const seq = this.currentAnimation.frameSequence;
    if (this.sequenceIndex + 1 < seq.length) {
      this.sequenceIndex++;
      this.currentFrameIndex = seq[this.sequenceIndex];
      this.notifyFrameChange();
      this.scheduleNextFrame();
    } else {
      // Reached the end of sequence
      const finishedAnim = this.currentAnimation.name;
      const nextAnim = this.currentAnimation.nextAnimation;
      const customOnComplete = this.currentAnimation.onComplete;

      this.emit('animationComplete', { animation: finishedAnim });

      if (customOnComplete) {
        customOnComplete();
      }

      if (this.currentAnimation.loop) {
        // Loop back to beginning
        this.sequenceIndex = 0;
        this.currentFrameIndex = seq[0];
        this.notifyFrameChange();
        this.scheduleNextFrame();
      } else if (nextAnim && this.animations.has(nextAnim)) {
        // Smoothly transition to next configured animation (e.g., kick -> idle)
        this.transitionTo(nextAnim);
      } else {
        // Stop on last frame
        this.isPlaying = false;
      }
    }
  }

  /**
   * Pause playback.
   */
  pause() {
    this.isPlaying = false;
    if (this.timerId) {
      clearTimeout(this.timerId);
      this.timerId = null;
    }
  }

  /**
   * Resume playback.
   */
  resume() {
    if (!this.isPlaying && this.currentAnimation) {
      this.isPlaying = true;
      this.scheduleNextFrame();
    }
  }

  /**
   * Stop and reset.
   */
  stop() {
    this.pause();
    this.sequenceIndex = 0;
    if (this.currentAnimation && this.currentAnimation.frameSequence.length > 0) {
      this.currentFrameIndex = this.currentAnimation.frameSequence[0];
      this.notifyFrameChange();
    }
  }

  /**
   * Step forward or backward by n frames.
   */
  stepFrame(delta = 1) {
    if (!this.currentAnimation) return;
    this.pause();
    const seq = this.currentAnimation.frameSequence;
    const newIdx = (this.sequenceIndex + delta + seq.length) % seq.length;
    this.sequenceIndex = newIdx;
    this.currentFrameIndex = seq[newIdx];
    this.notifyFrameChange();
  }

  /**
   * Set frame rate dynamically.
   */
  setFps(fps) {
    if (this.currentAnimation) {
      this.currentAnimation.fps = fps;
      if (this.isPlaying) {
        this.scheduleNextFrame();
      }
    }
  }

  /**
   * Set looping dynamically.
   */
  setLoop(loop) {
    if (this.currentAnimation) {
      this.currentAnimation.loop = !!loop;
    }
  }

  /**
   * Set custom frame sequence dynamically.
   */
  setFrameSequence(sequence) {
    if (this.currentAnimation && Array.isArray(sequence) && sequence.length > 0) {
      this.currentAnimation.frameSequence = [...sequence];
      this.sequenceIndex = 0;
      this.currentFrameIndex = sequence[0];
      this.notifyFrameChange();
    }
  }

  /**
   * Retrieve current frame data including metadata and anchor points.
   */
  getCurrentFrameData() {
    if (!this.currentAnimation) return null;
    const anim = this.currentAnimation;
    const meta = anim.frames && anim.frames[this.currentFrameIndex] ? anim.frames[this.currentFrameIndex] : null;
    const frameFile = meta ? meta.file : `frame_${String(this.currentFrameIndex).padStart(3, '0')}.png`;
    const relativePath = `${anim.framesDir}/${frameFile}`;

    return {
      animationName: anim.name,
      frameIndex: this.currentFrameIndex,
      sequenceIndex: this.sequenceIndex,
      totalSequenceFrames: anim.frameSequence.length,
      relativePath,
      meta,
      anchor: anim.anchor || this.globalAnchor,
      frameDimensions: this.frameDimensions
    };
  }

  notifyFrameChange() {
    const data = this.getCurrentFrameData();
    if (data) {
      this.emit('frameChange', data);
    }
  }

  on(event, handler) {
    if (this.listeners[event]) {
      this.listeners[event].push(handler);
    }
    return () => this.off(event, handler);
  }

  off(event, handler) {
    if (this.listeners[event]) {
      this.listeners[event] = this.listeners[event].filter(h => h !== handler);
    }
  }

  emit(event, payload) {
    if (this.listeners[event]) {
      for (const handler of this.listeners[event]) {
        try {
          handler(payload);
        } catch (err) {
          console.error(`Error in ${event} listener:`, err);
        }
      }
    }
  }
}

if (typeof module !== 'undefined' && module.exports) {
  module.exports = { SpriteAnimationController };
}
