// Web Desktop Pet Client - Multi-Character & Sword Power Studio
document.addEventListener('DOMContentLoaded', async () => {
  const canvas = document.getElementById('petCanvas');
  const ctx = canvas.getContext('2d');
  const stageWrapper = document.getElementById('stageWrapper');

  // UI Elements
  const charSelect = document.getElementById('charSelect');
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
  const actionButtonsContainer = document.getElementById('actionButtonsContainer');
  const actionHint = document.getElementById('actionHint');

  // Inspector Elements
  const metaChar = document.getElementById('metaChar');
  const metaAnim = document.getElementById('metaAnim');
  const metaFile = document.getElementById('metaFile');
  const metaBaseline = document.getElementById('metaBaseline');

  // Load configuration
  const response = await fetch('../animation-config.json');
  const config = await response.json();

  let activeCharacterId = config.defaultCharacter || 'hooded-blade';
  let activeCharDef = config.characters ? config.characters[activeCharacterId] : config;

  // Preload frames for all characters
  const imageCache = new Map();
  async function preloadAllFrames() {
    const promises = [];
    const characters = config.characters || { [activeCharacterId]: config };

    for (const [charId, character] of Object.entries(characters)) {
      for (const [animKey, anim] of Object.entries(character.animations)) {
        for (const frame of anim.frames) {
          const img = new Image();
          const src = `../${anim.framesDir}/${frame.file}`;
          img.src = src;
          const p = new Promise(resolve => {
            img.onload = () => {
              imageCache.set(`${charId}:${animKey}:${frame.index}`, img);
              resolve();
            };
            img.onerror = () => resolve();
          });
          promises.push(p);
        }
      }
    }
    await Promise.all(promises);
  }

  await preloadAllFrames();

  // Create controller
  let controller = new SpriteAnimationController(activeCharDef);

  let currentScale = 0.5;
  canvas.style.transform = 'scale(0.5)';
  let showDebug = false;
  let characterOffset = { x: 0, y: 0 };
  let isDragging = false;
  let dragStart = { x: 0, y: 0 };

  // Autonomous Roaming State
  let isAutonomous = true;
  let idleCountdown = Math.floor(Math.random() * 30 + 25);
  let runCountdown = 0;
  let targetPos = { x: 0, y: 0 };
  let moveSpeed = { x: 4.5, y: 1.5 };
  let facingLeft = false;

  function pickNewRunDestination() {
    targetPos.x = (Math.random() - 0.5) * 360;
    targetPos.y = (Math.random() - 0.5) * 120;
    moveSpeed.x = Math.random() * 3 + 4;
    moveSpeed.y = Math.random() * 1.5 + 1;
    runCountdown = Math.floor(Math.random() * 60 + 50);
  }

  function handleAutonomousLogic() {
    if (!isAutonomous || isDragging) return;

    const animName = controller.currentAnimation?.name || 'idle';
    const isBlade = (activeCharacterId === 'hooded-blade');

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
        const roll = Math.random() * 100;
        if (isBlade) {
          if (roll < 45) controller.play('sword-power'); // Unleash Sword Power!
          else if (roll < 75) controller.play('attack');  // Sword Slash
          else if (roll < 90) controller.play('jump');    // Leap Jump
          else {
            idleCountdown = Math.floor(Math.random() * 45 + 25);
            controller.play('idle');
          }
        } else {
          if (roll < 45) controller.play('kick');
          else if (roll < 80) controller.play('punch');
          else {
            idleCountdown = Math.floor(Math.random() * 45 + 25);
            controller.play('idle');
          }
        }
      }
    } else if (animName === 'idle') {
      idleCountdown--;
      if (idleCountdown <= 0) {
        const roll = Math.random() * 100;
        if (roll < 50) {
          pickNewRunDestination();
          controller.play('run');
        } else if (isBlade) {
          facingLeft = Math.random() < 0.5;
          if (roll < 75) controller.play('sword-power'); // 25% Sword Power!
          else if (roll < 90) controller.play('attack');
          else controller.play('jump');
        } else {
          facingLeft = Math.random() < 0.5;
          if (roll < 75) controller.play('kick');
          else controller.play('punch');
        }
      }
    }
  }

  function renderActionButtons() {
    actionButtonsContainer.innerHTML = '';
    const isBlade = (activeCharacterId === 'hooded-blade');

    const buttonDefs = isBlade ? [
      { id: 'sword-power', label: '⚡ Sword Power', primary: true },
      { id: 'attack', label: '🗡️ Sword Slash' },
      { id: 'jump', label: '🦘 Leap Jump' },
      { id: 'run', label: '🏃 Run Sprint' },
      { id: 'idle', label: '🧍 Idle Stance' }
    ] : [
      { id: 'kick', label: '🥋 Kick Action', primary: true },
      { id: 'punch', label: '🥊 Punch Action' },
      { id: 'run', label: '🏃 Run Sprint' },
      { id: 'idle', label: '🧍 Idle Stance' }
    ];

    actionHint.innerHTML = isBlade
      ? '* Note: <strong>Sword Power</strong> charges and unleashes an emerald shockwave, smoothly transitioning back to <strong>Idle</strong>.'
      : '* Note: <strong>Kick</strong> and <strong>Punch</strong> smoothly transition back to <strong>Idle</strong> upon completion.';

    for (const def of buttonDefs) {
      const btn = document.createElement('button');
      btn.id = `btnAct_${def.id}`;
      btn.textContent = def.label;
      if (def.primary) btn.classList.add('primary');
      if (def.id === 'idle') btn.classList.add('active-anim');

      btn.addEventListener('click', () => {
        if (def.id === 'run') pickNewRunDestination();
        controller.play(def.id);
      });
      actionButtonsContainer.appendChild(btn);
    }
  }

  function updateActionButtonsHighlight(activeName) {
    currentAnimBadge.textContent = activeName.toUpperCase();
    const buttons = actionButtonsContainer.querySelectorAll('button');
    buttons.forEach(btn => {
      if (btn.id === `btnAct_${activeName}`) {
        btn.classList.add('active-anim');
      } else {
        btn.classList.remove('active-anim');
      }
    });

    const anim = controller.getAnimation(activeName);
    if (anim) {
      chkLoop.checked = anim.loop;
      sliderFps.value = anim.fps;
      lblFps.textContent = `${anim.fps} FPS`;
      txtSequence.value = anim.frameSequence.join(', ');
    }
  }

  function switchActiveCharacter(charId) {
    if (!config.characters || !config.characters[charId]) return;
    activeCharacterId = charId;
    activeCharDef = config.characters[charId];
    charSelect.value = charId;

    metaChar.textContent = activeCharDef.name;
    controller.loadConfig(activeCharDef);
    renderActionButtons();

    idleCountdown = Math.floor(Math.random() * 25 + 20);
    controller.play(activeCharDef.defaultAnimation || 'idle');
  }

  // Draw current frame on canvas
  function renderFrame(frameData) {
    if (!frameData) return;
    const { animationName, frameIndex, sequenceIndex, totalSequenceFrames, meta, anchor } = frameData;

    handleAutonomousLogic();

    ctx.clearRect(0, 0, canvas.width, canvas.height);

    const img = imageCache.get(`${activeCharacterId}:${animationName}:${frameIndex}`);
    if (img) {
      ctx.save();
      ctx.translate(characterOffset.x + canvas.width / 2, characterOffset.y + canvas.height / 2);
      ctx.scale(facingLeft ? -1 : 1, 1);
      ctx.drawImage(img, -canvas.width / 2, -canvas.height / 2, canvas.width, canvas.height);

      if (showDebug && meta) {
        ctx.strokeStyle = '#ef4444';
        ctx.lineWidth = 1;
        ctx.beginPath();
        ctx.moveTo(-canvas.width / 2, anchor.y - canvas.height / 2);
        ctx.lineTo(canvas.width / 2, anchor.y - canvas.height / 2);
        ctx.stroke();

        ctx.strokeStyle = '#38bdf8';
        ctx.lineWidth = 2;
        ctx.beginPath();
        ctx.arc(anchor.x - canvas.width / 2, anchor.y - canvas.height / 2, 6, 0, Math.PI * 2);
        ctx.stroke();
      }
      ctx.restore();
    }

    lblFrame.textContent = `${sequenceIndex + 1} / ${totalSequenceFrames}`;
    const duration = (totalSequenceFrames / (controller.currentAnimation?.fps || 24)).toFixed(1);
    const currentTime = (sequenceIndex / (controller.currentAnimation?.fps || 24)).toFixed(2);
    lblTime.textContent = `${currentTime}s / ${duration}s`;

    metaAnim.textContent = animationName;
    metaFile.textContent = meta ? meta.file : `frame_${frameIndex}.png`;
    metaBaseline.textContent = `y = ${anchor.y} px`;
  }

  controller.on('frameChange', frameData => renderFrame(frameData));
  controller.on('animationStart', e => updateActionButtonsHighlight(e.animation));

  charSelect.addEventListener('change', e => {
    switchActiveCharacter(e.target.value);
  });

  // Click canvas character to trigger primary power action!
  canvas.addEventListener('click', () => {
    if (!isDragging) {
      if (activeCharacterId === 'hooded-blade') {
        controller.play('sword-power');
      } else {
        controller.play('kick');
      }
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

  sliderFps.addEventListener('input', e => {
    const fps = parseInt(e.target.value, 10);
    lblFps.textContent = `${fps} FPS`;
    controller.setFps(fps);
  });

  sliderScale.addEventListener('input', e => {
    currentScale = parseFloat(e.target.value);
    lblScale.textContent = `${currentScale.toFixed(1)}x (${Math.round(512 * currentScale)}x${Math.round(512 * currentScale)})`;
    canvas.style.transform = `scale(${currentScale})`;
  });

  chkAutonomous.addEventListener('change', e => {
    isAutonomous = e.target.checked;
    if (!isAutonomous && controller.currentAnimation?.name === 'run') {
      controller.play('idle');
    }
  });

  chkLoop.addEventListener('change', e => controller.setLoop(e.target.checked));
  chkDebug.addEventListener('change', e => {
    showDebug = e.target.checked;
    renderFrame(controller.getCurrentFrameData());
  });

  btnApplySeq.addEventListener('click', () => {
    const raw = txtSequence.value;
    const parts = raw.split(',').map(s => parseInt(s.trim(), 10)).filter(n => !isNaN(n));
    if (parts.length > 0) controller.setFrameSequence(parts);
  });

  btnResetSeq.addEventListener('click', () => {
    if (controller.currentAnimation) {
      const orig = activeCharDef.animations[controller.currentAnimation.name];
      if (orig) {
        controller.setFrameSequence(orig.frameSequence);
        txtSequence.value = orig.frameSequence.join(', ');
      }
    }
  });

  bgSelect.addEventListener('change', e => {
    stageWrapper.className = `stage-wrapper ${e.target.value}`;
  });

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

  window.addEventListener('keydown', e => {
    if (e.key === 'c' || e.key === 'C') {
      const nextChar = (activeCharacterId === 'hooded-blade') ? 'robot' : 'hooded-blade';
      switchActiveCharacter(nextChar);
    } else if (e.code === 'Space' || e.key === 's' || e.key === 'S') {
      if (activeCharacterId === 'hooded-blade') controller.play('sword-power');
      else controller.play('kick');
    } else if (e.key === 'a' || e.key === 'A') {
      if (activeCharacterId === 'hooded-blade') controller.play('attack');
      else controller.play('punch');
    } else if (e.key === 'j' || e.key === 'J') {
      if (activeCharacterId === 'hooded-blade') controller.play('jump');
    } else if (e.key === 'r' || e.key === 'R') {
      pickNewRunDestination();
      controller.play('run');
    } else if (e.key === 'i' || e.key === 'I') {
      controller.play('idle');
    }
  });

  // Initialize UI
  switchActiveCharacter(activeCharacterId);
});
