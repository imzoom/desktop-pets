// Web Desktop Pet Client
document.addEventListener('DOMContentLoaded', async () => {
  const canvas = document.getElementById('petCanvas');
  const ctx = canvas.getContext('2d');
  const stageWrapper = document.getElementById('stageWrapper');

  // UI Elements
  const currentAnimBadge = document.getElementById('currentAnimBadge');
  const lblFrame = document.getElementById('lblFrame');
  const lblTime = document.getElementById('lblTime');
  const lblFps = document.getElementById('lblFps');
  const lblScale = document.getElementById('lblScale');
  const sliderFps = document.getElementById('sliderFps');
  const sliderScale = document.getElementById('sliderScale');
  const chkAutonomous = document.getElementById('chkAutonomous');
  const chkLoop = document.getElementById('chkLoop');
  const chkDebug = document.getElementById('chkDebug');
  const txtSequence = document.getElementById('txtSequence');
  const btnPlayPause = document.getElementById('btnPlayPause');
  const btnStepPrev = document.getElementById('btnStepPrev');
  const btnStepNext = document.getElementById('btnStepNext');
  const btnApplySeq = document.getElementById('btnApplySeq');
  const btnResetSeq = document.getElementById('btnResetSeq');
  const bgSelect = document.getElementById('bgSelect');

  // Action buttons
  const actionButtons = {
    idle: document.getElementById('btnActIdle'),
    kick: document.getElementById('btnActKick'),
    punch: document.getElementById('btnActPunch'),
    run: document.getElementById('btnActRun')
  };

  // Inspector Elements
  const metaAnim = document.getElementById('metaAnim');
  const metaFile = document.getElementById('metaFile');
  const metaBaseline = document.getElementById('metaBaseline');
  const metaAnchor = document.getElementById('metaAnchor');
  const metaBox = document.getElementById('metaBox');

  // Load animation configuration
  const response = await fetch('../animation-config.json');
  const config = await response.json();

  // Preload images into memory for instant rendering
  const imageCache = new Map();
  async function preloadAllFrames() {
    const promises = [];
    for (const [key, anim] of Object.entries(config.animations)) {
      for (const frame of anim.frames) {
        const img = new Image();
        const src = `../${anim.framesDir}/${frame.file}`;
        img.src = src;
        const p = new Promise(resolve => {
          img.onload = () => {
            imageCache.set(`${key}:${frame.index}`, img);
            resolve();
          };
          img.onerror = () => resolve();
        });
        promises.push(p);
      }
    }
    await Promise.all(promises);
  }

  await preloadAllFrames();

  // Create reusable controller
  const controller = new SpriteAnimationController(config);

  let currentScale = 0.5;
  canvas.style.transform = 'scale(0.5)';
  let showDebug = false;
  let characterOffset = { x: 0, y: 0 };
  let isDragging = false;
  let dragStart = { x: 0, y: 0 };

  // Autonomous Roaming & Random Actions State
  let isAutonomous = true;
  let idleCountdown = Math.floor(Math.random() * 40 + 30);
  let runCountdown = 0;
  let targetPos = { x: 0, y: 0 };
  let moveSpeed = { x: 4.5, y: 1.5 };
  let facingLeft = false;

  function pickNewRunDestination() {
    // Wander within the stage bounds (-180 to +180 px horizontal, -60 to +60 px vertical)
    targetPos.x = (Math.random() - 0.5) * 360;
    targetPos.y = (Math.random() - 0.5) * 120;
    moveSpeed.x = Math.random() * 3 + 4; // 4 to 7 px per frame
    moveSpeed.y = Math.random() * 1.5 + 1; // 1 to 2.5 px per frame
    runCountdown = Math.floor(Math.random() * 60 + 50); // 2 to 4.5 seconds of run
  }

  function handleAutonomousLogic() {
    if (!isAutonomous || isDragging) return;

    const animName = controller.currentAnimation?.name || 'idle';

    if (animName === 'run') {
      const dx = targetPos.x - characterOffset.x;
      const dy = targetPos.y - characterOffset.y;
      const dist = Math.hypot(dx, dy);

      if (dist > 8 && runCountdown > 0) {
        runCountdown--;
        const stepX = Math.sign(dx) * Math.min(Math.abs(dx), moveSpeed.x);
        const stepY = Math.sign(dy) * Math.min(Math.abs(dy), moveSpeed.y);

        if (stepX < -0.5) facingLeft = true;
        else if (stepX > 0.5) facingLeft = false;

        characterOffset.x = Math.max(-200, Math.min(200, characterOffset.x + stepX));
        characterOffset.y = Math.max(-80, Math.min(80, characterOffset.y + stepY));
      } else {
        // Arrived at destination or finished sprint!
        // Randomly pick next action: Kick (40%), Punch (40%), Idle (20%)
        const roll = Math.random() * 100;
        if (roll < 40) {
          controller.play('kick');
        } else if (roll < 80) {
          controller.play('punch');
        } else {
          idleCountdown = Math.floor(Math.random() * 50 + 30);
          controller.play('idle');
        }
      }
    } else if (animName === 'idle') {
      idleCountdown--;
      if (idleCountdown <= 0) {
        // Pick random next action
        const roll = Math.random() * 100;
        if (roll < 55) {
          pickNewRunDestination();
          controller.play('run');
        } else if (roll < 80) {
          facingLeft = Math.random() < 0.5;
          controller.play('kick');
        } else {
          facingLeft = Math.random() < 0.5;
          controller.play('punch');
        }
      }
    }
  }

  function updateActionButtons(activeName) {
    currentAnimBadge.textContent = activeName.toUpperCase();
    for (const [name, btn] of Object.entries(actionButtons)) {
      if (name === activeName) {
        btn.classList.add('active-anim');
      } else {
        btn.classList.remove('active-anim');
      }
    }
    const anim = controller.getAnimation(activeName);
    if (anim) {
      chkLoop.checked = anim.loop;
      sliderFps.value = anim.fps;
      lblFps.textContent = `${anim.fps} FPS`;
      txtSequence.value = anim.frameSequence.join(', ');
    }
  }

  // Draw current frame on canvas
  function renderFrame(frameData) {
    if (!frameData) return;
    const { animationName, frameIndex, sequenceIndex, totalSequenceFrames, meta, anchor } = frameData;

    // Run autonomous tick if enabled
    handleAutonomousLogic();

    // Clear canvas completely to preserve transparency
    ctx.clearRect(0, 0, canvas.width, canvas.height);

    const img = imageCache.get(`${animationName}:${frameIndex}`);
    if (img) {
      ctx.save();
      // Apply pet dragging / wandering offset
      ctx.translate(characterOffset.x + canvas.width / 2, characterOffset.y + canvas.height / 2);

      // Apply horizontal facing direction
      ctx.scale(facingLeft ? -1 : 1, 1);

      // Draw centered around the middle of canvas
      ctx.drawImage(img, -canvas.width / 2, -canvas.height / 2, canvas.width, canvas.height);

      // Render Debug overlay if enabled
      if (showDebug && meta) {
        // Ground Baseline
        ctx.strokeStyle = '#ef4444';
        ctx.lineWidth = 1;
        ctx.beginPath();
        ctx.moveTo(-canvas.width / 2, anchor.y - canvas.height / 2);
        ctx.lineTo(canvas.width / 2, anchor.y - canvas.height / 2);
        ctx.stroke();

        // Anchor Crosshair
        ctx.strokeStyle = '#38bdf8';
        ctx.lineWidth = 2;
        ctx.beginPath();
        ctx.arc(anchor.x - canvas.width / 2, anchor.y - canvas.height / 2, 6, 0, Math.PI * 2);
        ctx.stroke();

        // Content Bounding Box
        ctx.strokeStyle = '#34d399';
        ctx.lineWidth = 1;
        ctx.strokeRect(
          meta.contentBox.minX - canvas.width / 2,
          meta.contentBox.minY - canvas.height / 2,
          meta.contentBox.width,
          meta.contentBox.height
        );
      }
      ctx.restore();
    }

    // Update UI info
    lblFrame.textContent = `${sequenceIndex + 1} / ${totalSequenceFrames}`;
    const duration = (totalSequenceFrames / (controller.currentAnimation?.fps || 24)).toFixed(1);
    const currentTime = (sequenceIndex / (controller.currentAnimation?.fps || 24)).toFixed(2);
    lblTime.textContent = `${currentTime}s / ${duration}s`;

    metaAnim.textContent = animationName;
    metaFile.textContent = meta ? meta.file : `frame_${frameIndex}.png`;
    metaBaseline.textContent = `y = ${anchor.y} px`;
    metaAnchor.textContent = `x = ${anchor.x}, y = ${anchor.y}`;
    if (meta && meta.contentBox) {
      metaBox.textContent = `${meta.contentBox.width} × ${meta.contentBox.height} px`;
    }
  }

  // Controller event listeners
  controller.on('frameChange', frameData => {
    renderFrame(frameData);
  });

  controller.on('animationStart', e => {
    updateActionButtons(e.animation);
  });

  controller.on('animationComplete', e => {
    if (e.animation === 'kick' || e.animation === 'punch') {
      // After kick or punch finishes, set idle duration before next autonomous action
      idleCountdown = Math.floor(Math.random() * 50 + 30);
    }
  });

  controller.on('transition', e => {
    console.log(`Transitioning: ${e.from} -> ${e.to}`);
  });

  // Action Button Listeners
  actionButtons.kick.addEventListener('click', () => {
    controller.play('kick');
  });

  actionButtons.idle.addEventListener('click', () => {
    controller.play('idle');
  });

  actionButtons.punch.addEventListener('click', () => {
    controller.play('punch');
  });

  actionButtons.run.addEventListener('click', () => {
    pickNewRunDestination();
    controller.play('run');
  });

  // Click on canvas character to trigger Kick action!
  canvas.addEventListener('click', () => {
    if (!isDragging) {
      controller.play('kick');
    }
  });

  // Playback control buttons
  btnPlayPause.addEventListener('click', () => {
    if (controller.isPlaying) {
      controller.pause();
      btnPlayPause.textContent = '▶ Play';
      btnPlayPause.classList.remove('primary');
    } else {
      controller.resume();
      btnPlayPause.textContent = '⏸ Pause';
      btnPlayPause.classList.add('primary');
    }
  });

  btnStepPrev.addEventListener('click', () => {
    controller.stepFrame(-1);
    btnPlayPause.textContent = '▶ Play';
  });

  btnStepNext.addEventListener('click', () => {
    controller.stepFrame(1);
    btnPlayPause.textContent = '▶ Play';
  });

  // Settings
  sliderFps.addEventListener('input', e => {
    const fps = parseInt(e.target.value, 10);
    lblFps.textContent = `${fps} FPS`;
    controller.setFps(fps);
  });

  sliderScale.addEventListener('input', e => {
    currentScale = parseFloat(e.target.value);
    lblScale.textContent = `${currentScale.toFixed(1)}x (${Math.round(512 * currentScale)}x${Math.round(384 * currentScale)})`;
    canvas.style.transform = `scale(${currentScale})`;
  });

  chkAutonomous.addEventListener('change', e => {
    isAutonomous = e.target.checked;
    if (!isAutonomous && controller.currentAnimation?.name === 'run') {
      controller.play('idle');
    }
  });

  chkLoop.addEventListener('change', e => {
    controller.setLoop(e.target.checked);
  });

  chkDebug.addEventListener('change', e => {
    showDebug = e.target.checked;
    renderFrame(controller.getCurrentFrameData());
  });

  // Custom Sequence
  btnApplySeq.addEventListener('click', () => {
    const raw = txtSequence.value;
    const parts = raw.split(',').map(s => parseInt(s.trim(), 10)).filter(n => !isNaN(n));
    if (parts.length > 0) {
      controller.setFrameSequence(parts);
    }
  });

  btnResetSeq.addEventListener('click', () => {
    if (controller.currentAnimation) {
      const orig = config.animations[controller.currentAnimation.name];
      if (orig) {
        controller.setFrameSequence(orig.frameSequence);
        txtSequence.value = orig.frameSequence.join(', ');
      }
    }
  });

  // Backdrop switcher
  bgSelect.addEventListener('change', e => {
    stageWrapper.className = `stage-wrapper ${e.target.value}`;
  });

  // Dragging character on stage
  canvas.addEventListener('mousedown', e => {
    isDragging = true;
    dragStart = { x: e.clientX - characterOffset.x, y: e.clientY - characterOffset.y };
  });

  window.addEventListener('mousemove', e => {
    if (isDragging) {
      characterOffset.x = e.clientX - dragStart.x;
      characterOffset.y = e.clientY - dragStart.y;
      renderFrame(controller.getCurrentFrameData());
    }
  });

  window.addEventListener('mouseup', () => {
    setTimeout(() => { isDragging = false; }, 50);
  });

  // Keyboard controls
  window.addEventListener('keydown', e => {
    if (e.code === 'Space' || e.key === 'k') {
      controller.play('kick');
    } else if (e.key === 'p') {
      controller.play('punch');
    } else if (e.key === 'r') {
      pickNewRunDestination();
      controller.play('run');
    } else if (e.key === 'i') {
      controller.play('idle');
    }
  });

  // Start with Idle animation
  controller.play('idle');
});
