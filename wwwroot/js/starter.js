/* One-time character choices are persisted by the server, never localStorage. */
(function () {
  let owner = null, dialog = null, requestedPanel = null;
  const escape = value => String(value ?? '').replace(/[&<>"']/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));

  window.renderStarterChoice = function () {
    const choice = S?.starterChoice;
    if (!choice?.required) {
      if (dialog) { dialog.close(); dialog.remove(); dialog = null; }
      document.getElementById('starterStripPrompt')?.remove();
      owner = null;
      return;
    }
    if (MODE === 'strip') {
      if (!document.getElementById('starterStripPrompt')) {
        const button = document.createElement('button'); button.id = 'starterStripPrompt';
        button.textContent = 'Chọn công pháp và linh thú khởi đầu';
        button.onclick = () => window.chrome?.webview ? notifyHost('open-panel', {tab:'skill'}) : window.open('?mode=panel&tab=skill', 'van-dao-starter', 'width=960,height=760');
        document.body.appendChild(button);
        if (requestedPanel !== S.id && window.chrome?.webview) { requestedPanel = S.id; button.click(); }
      }
      return;
    }
    if (dialog && owner === S.id) return;
    if (dialog) { dialog.close(); dialog.remove(); }
    owner = S.id;
    dialog = document.createElement('dialog'); dialog.id = 'starterChoice';
    dialog.setAttribute('aria-labelledby', 'starterTitle');
    const manualCards = choice.manuals.map(m => {
      const element = DATA.elements.find(e => e.id === m.element);
      return `<label class="starter-card"><input type="radio" name="starterManual" value="${escape(m.element)}" required><span class="starter-card-content"><img src="${escape(ITEM[m.item].icon)}" alt="" style="filter:${escape(ITEM[m.item].tint || 'none')}"><span><b>${escape(element?.name)}</b></span></span></label>`;
    }).join('');
    const petCards = choice.pets.map(option => {
      const pet = DATA.petDefs.find(p => p.id === option.species);
      return `<label class="starter-card"><input type="radio" name="starterEgg" value="${escape(option.egg)}" required><span class="starter-card-content"><img class="starter-pet-art" src="${escape(pet.forms[0].image)}" alt=""><span><b>${escape(pet.name)}</b></span></span></label>`;
    }).join('');
    dialog.innerHTML = `<form class="starter-form"><header><small>VẤN ĐẠO · KHỞI ĐẦU</small><h1 id="starterTitle">Chọn công pháp</h1></header><nav class="starter-steps" aria-label="Các bước khởi đầu"><span data-step-marker="0" aria-current="step">1. Công pháp</span><span data-step-marker="1">2. Linh thú</span></nav><div class="starter-body"><fieldset data-starter-step="0"><legend class="starter-sr-only">Công pháp khởi đầu</legend><div class="starter-options">${manualCards}</div></fieldset><fieldset data-starter-step="1" hidden><legend class="starter-sr-only">Linh thú khởi đầu</legend><div class="starter-options">${petCards}</div></fieldset><section class="starter-detail" aria-live="polite"><h2>Chọn một hệ công pháp</h2><p>Mở quyền học toàn bộ cây đã chọn. Nâng kỹ năng vẫn cần điểm và cảnh giới.</p></section></div><footer><p class="starter-error" role="alert"></p><div class="starter-navigation"><button type="button" data-starter-back hidden>Quay lại</button><button type="button" data-starter-next disabled>Tiếp tục · Chọn linh thú</button><button type="submit" hidden disabled>Bắt đầu tu luyện</button></div></footer></form>`;
    const current = dialog, form = dialog.querySelector('form'), submit = form.querySelector('button[type=submit]');
    const next = form.querySelector('[data-starter-next]'), back = form.querySelector('[data-starter-back]');
    let step = 0;
    function update() {
      const values = new FormData(form), manual = values.get('starterManual'), egg = values.get('starterEgg');
      const m = choice.manuals.find(x => x.element === manual), p = choice.pets.find(x => x.egg === egg);
      const detail = form.querySelector('.starter-detail');
      form.querySelector('#starterTitle').textContent = step === 0 ? 'Chọn công pháp' : 'Chọn linh thú đồng hành';
      form.querySelectorAll('[data-starter-step]').forEach(el => el.hidden = Number(el.dataset.starterStep) !== step);
      form.querySelectorAll('[data-step-marker]').forEach(el => {
        if (Number(el.dataset.stepMarker) === step) el.setAttribute('aria-current', 'step'); else el.removeAttribute('aria-current');
      });
      next.hidden = step !== 0; back.hidden = step !== 1; submit.hidden = step !== 1;
      next.disabled = !m; submit.disabled = !m || !p;
      if (step === 0) {
        const el = DATA.elements.find(x => x.id === m?.element);
        detail.innerHTML = `<h2>${escape(m?.name || 'Chọn một hệ công pháp')}</h2>${el ? '<p>'+escape(el.desc)+'</p>' : ''}<p>Mở quyền học toàn bộ cây. Nâng kỹ năng vẫn cần điểm và cảnh giới.</p>`;
      } else {
        const pet = DATA.petDefs.find(x => x.id === p?.species);
        detail.innerHTML = `<h2>${escape(pet?.name || 'Chọn một linh thú')}</h2>${pet ? '<p>'+escape(pet.talentText)+'</p><p>'+escape(pet.skillText)+'</p><small>Trứng nở đúng loài · Ấp '+Math.round(p.hatchSeconds/60)+' phút</small>' : '<p>Chỉ chọn phẩm Phàm hoặc Linh. Nhận một trứng nở đúng loài, đem đến mục Linh thú để ấp.</p>'}`;
      }
    }
    form.addEventListener('change', update);
    next.addEventListener('click', () => { if (next.disabled) return; step = 1; update(); form.querySelector('input[name=starterEgg]:checked,input[name=starterEgg]').focus(); });
    back.addEventListener('click', () => { step = 0; update(); form.querySelector('input[name=starterManual]:checked').focus(); });
    form.addEventListener('submit', async event => {
      event.preventDefault(); if (step !== 1) { if (!next.disabled) next.click(); return; } if (busy || submit.disabled) return;
      const values = new FormData(form);
      submit.disabled = true; submit.textContent = 'Đang lưu lựa chọn…';
      const ok = await act(`starter?element=${encodeURIComponent(values.get('starterManual'))}&egg=${encodeURIComponent(values.get('starterEgg'))}`);
      if (!ok && dialog === current) {
        form.querySelector('.starter-error').textContent = ddErr?.msg || 'Chưa lưu được lựa chọn. Vui lòng thử lại.';
        submit.disabled = false; submit.textContent = 'Bắt đầu tu luyện';
      }
    });
    dialog.addEventListener('cancel', event => event.preventDefault());
    document.body.appendChild(dialog); dialog.showModal();
  };
})();
