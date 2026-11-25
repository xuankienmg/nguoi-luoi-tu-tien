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
    // Một màn duy nhất: đạo hiệu + công pháp + linh thú, bấm một nút là vào game.
    dialog.innerHTML = `<form class="starter-form"><header><small>KHỞI ĐẦU</small><h1 id="starterTitle">Tạo nhân vật</h1></header><div class="starter-body"><label class="starter-name"><span>Đạo hiệu</span><input name="starterName" maxlength="20" autocomplete="off" required placeholder="2–20 ký tự" value="${escape(S.name === 'Đạo hữu' ? '' : S.name)}"><button type="button" class="starter-random" data-starter-random>Ngẫu nhiên</button></label><div class="starter-picks"><fieldset class="starter-manuals"><legend>Công pháp</legend><div class="starter-options">${manualCards}</div><p class="starter-note" data-note="manual" aria-live="polite"></p></fieldset><fieldset class="starter-pets"><legend>Linh thú đồng hành</legend><div class="starter-options">${petCards}</div><p class="starter-note" data-note="pet" aria-live="polite"></p></fieldset></div></div><footer><p class="starter-hint" aria-live="polite"></p><p class="starter-error" role="alert"></p><div class="starter-navigation"><button type="submit" disabled>Bắt đầu tu luyện</button></div></footer></form>`;
    const current = dialog, form = dialog.querySelector('form'), submit = form.querySelector('button[type=submit]');
    // Đạo hiệu gợi ý: chỉ điền sẵn ô nhập, người chơi vẫn sửa được; server vẫn kiểm tra như tên tự gõ.
    const SURNAMES = ['Lâm', 'Tiêu', 'Lý', 'Hàn', 'Diệp', 'Tần', 'Vân', 'Bạch', 'Lục', 'Tô', 'Cố', 'Mộ Dung', 'Thượng Quan', 'Âu Dương'];
    const GIVEN = ['Phong', 'Thanh Huyền', 'Vô Cực', 'Minh Nguyệt', 'Dật Trần', 'Tử Yên', 'Hạo Thiên', 'Tiêu Dao', 'Nhược Thủy', 'Trường Sinh', 'Lạc Hà', 'Kinh Vũ', 'Thiên Hành', 'Vân Khê'];
    const pick = list => list[Math.floor(Math.random() * list.length)];
    form.querySelector('[data-starter-random]').addEventListener('click', () => {
      const input = form.querySelector('input[name=starterName]');
      input.value = `${pick(SURNAMES)} ${pick(GIVEN)}`; update();
    });
    // giống GameService.CleanName: gộp khoảng trắng, 2–20 ký tự, không ký tự điều khiển hay <>"'`
    const cleanName = value => String(value ?? '').split(' ').filter(Boolean).join(' ');
    const nameOk = value => { const n = cleanName(value); return n.length >= 2 && n.length <= 20 && !/[\u0000-\u001f\u007f<>"'`]/.test(n); };
    function update() {
      const values = new FormData(form), manual = values.get('starterManual'), egg = values.get('starterEgg');
      const m = choice.manuals.find(x => x.element === manual), p = choice.pets.find(x => x.egg === egg);
      const named = nameOk(values.get('starterName'));
      submit.disabled = !m || !p || !named;
      // Nói rõ còn thiếu gì thay vì chỉ làm mờ nút; mỗi mục một <span> để i18n dịch được.
      const missing = [!named && 'đạo hiệu (2–20 ký tự)', !m && 'công pháp', !p && 'linh thú'].filter(Boolean);
      form.querySelector('.starter-hint').innerHTML = missing.length ? `<span>Còn thiếu:</span> ${missing.map(x => `<span>${x}</span>`).join(' · ')}` : '';
      const el = DATA.elements.find(x => x.id === m?.element);
      form.querySelector('[data-note=manual]').innerHTML = m
        ? `<b>${escape(m.name)}</b>${el ? ' — ' + escape(el.desc) : ''}`
        : 'Mở quyền học toàn bộ cây đã chọn. Nâng kỹ năng vẫn cần điểm và cảnh giới.';
      const pet = DATA.petDefs.find(x => x.id === p?.species);
      form.querySelector('[data-note=pet]').innerHTML = pet
        ? `<b>${escape(pet.name)}</b> — ${escape(pet.talentText)} ${escape(pet.skillText)} <small><span>Ấp</span> ${Math.round(p.hatchSeconds / 60)} <span>phút</span></small>`
        : 'Nhận một trứng nở đúng loài (phẩm Phàm hoặc Linh), đem đến mục Linh thú để ấp.';
    }
    form.addEventListener('change', update);
    form.addEventListener('input', update);
    form.addEventListener('submit', async event => {
      event.preventDefault(); if (busy || submit.disabled) return;
      const values = new FormData(form);
      submit.disabled = true; submit.textContent = 'Đang lưu lựa chọn…';
      const ok = await act(`starter?${new URLSearchParams({element: values.get('starterManual'), egg: values.get('starterEgg'), name: cleanName(values.get('starterName'))})}`);
      if (!ok && dialog === current) {
        form.querySelector('.starter-error').textContent = ddErr?.msg || 'Chưa lưu được lựa chọn. Vui lòng thử lại.';
        submit.disabled = false; submit.textContent = 'Bắt đầu tu luyện';
      }
    });
    dialog.addEventListener('cancel', event => event.preventDefault());
    update();
    document.body.appendChild(dialog); dialog.showModal();
  };
})();
