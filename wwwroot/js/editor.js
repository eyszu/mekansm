(() => {
  const body = document.getElementById('Input_Body');
  const preview = document.getElementById('preview');
  const form = document.getElementById('editor-form');
  if (!body || !preview || !form) return;

  const token = form.querySelector('input[name="__RequestVerificationToken"]')?.value ?? '';
  let timer = 0;
  let seq = 0;

  async function render() {
    const mine = ++seq;
    try {
      const res = await fetch('/api/preview', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', RequestVerificationToken: token },
        body: JSON.stringify({ markdown: body.value }),
      });
      if (!res.ok || mine !== seq) return;
      preview.innerHTML = await res.text(); // sanitized server-side
    } catch { /* offline: keep last preview */ }
  }

  body.addEventListener('input', () => { clearTimeout(timer); timer = setTimeout(render, 250); });
  render();

  document.querySelector('.toolbar[role="toolbar"]')?.addEventListener('click', (e) => {
    const btn = e.target.closest('button[data-before]');
    if (!btn) return;
    const before = btn.dataset.before;
    const after = btn.dataset.after ?? '';
    const { selectionStart: s, selectionEnd: en, value } = body;
    const selected = value.slice(s, en);
    body.setRangeText(before + selected + after, s, en, 'select');
    body.focus();
    body.setSelectionRange(s + before.length, s + before.length + selected.length);
    body.dispatchEvent(new Event('input'));
  });
})();
