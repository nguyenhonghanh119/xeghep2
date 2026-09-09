function formatVnd(value) {
  return Math.round(Number(value) || 0).toLocaleString('vi-VN') + 'đ';
}

function showToast(message, type = 'success') {
  const toast = document.createElement('div');
  toast.textContent = message;
  toast.setAttribute('role', 'status');
  toast.style.cssText = `position:fixed;right:18px;top:18px;z-index:9999;padding:11px 15px;border-radius:9px;color:#fff;background:${type === 'error' ? '#c0392b' : '#237a57'};box-shadow:0 8px 24px rgba(0,0,0,.18)`;
  document.body.appendChild(toast);
  setTimeout(() => toast.remove(), 3000);
}

// Hộp xác nhận dùng chung, thay cho window.confirm để giao diện đồng nhất với showToast().
function askConfirm(message, { confirmText = 'Xác nhận', cancelText = 'Hủy' } = {}) {
  return new Promise(resolve => {
    const overlay = document.createElement('div');
    Object.assign(overlay.style, {
      position: 'fixed', inset: '0', zIndex: '9999', background: 'rgba(24,31,42,.45)',
      display: 'grid', placeItems: 'center', padding: '16px'
    });
    const box = document.createElement('div');
    Object.assign(box.style, { background: '#fff', borderRadius: '14px', padding: '20px', width: 'min(400px,100%)', boxShadow: '0 18px 50px rgba(0,0,0,.2)' });
    const text = document.createElement('div');
    text.textContent = message;
    text.style.cssText = 'font-weight:600;line-height:1.5;margin-bottom:16px';
    const actions = document.createElement('div');
    actions.style.cssText = 'display:flex;justify-content:flex-end;gap:8px';
    const cancel = document.createElement('button');
    cancel.className = 'btn btn-outline btn-sm'; cancel.textContent = cancelText;
    const accept = document.createElement('button');
    accept.className = 'btn btn-primary btn-sm'; accept.textContent = confirmText;
    actions.append(cancel, accept); box.append(text, actions); overlay.appendChild(box); document.body.appendChild(overlay);
    const finish = value => { overlay.remove(); resolve(value); };
    cancel.onclick = () => finish(false);
    accept.onclick = () => finish(true);
    overlay.addEventListener('click', event => { if (event.target === overlay) finish(false); });
    overlay.addEventListener('keydown', event => { if (event.key === 'Escape') finish(false); });
    accept.focus();
  });
}
