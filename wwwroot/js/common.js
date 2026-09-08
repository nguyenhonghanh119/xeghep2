// XeGhép — Quản trị — JS dùng chung cho mọi trang (tương đương assets/common.js trong bản PHP gốc)

// Đóng/mở nhóm menu con trong sidebar (vd: "Quản lý người dùng").
function toggleUserMenu() {
  const menu = document.getElementById('userMenu');
  if (menu) menu.classList.toggle('open');
}

// Helper dùng chung: cập nhật số trên các badge-count trong sidebar (vd: số hồ sơ tài xế chờ duyệt).
function updateSidebarBadge(navHref, delta) {
  document.querySelectorAll(`.side-nav a[href="${navHref}"] .badge-count`).forEach(el => {
    const n = Math.max(0, parseInt(el.textContent, 10) + delta);
    if (n === 0) { el.remove(); } else { el.textContent = n; }
  });
}

// Định dạng số tiền kiểu Việt Nam, dùng cho các ô tính giá đề xuất phía client.
function formatVnd(soTien) {
  return Math.round(soTien).toLocaleString('vi-VN') + 'đ';
}
