// Helper dùng chung cho các trang hành khách — gọi API thật /api/passenger/* thay cho dữ liệu demo.
async function apiFetch(url, options = {}) {
  const res = await fetch(url, {
    headers: { 'Content-Type': 'application/json' },
    ...options,
  });
  let data = null;
  try { data = await res.json(); } catch (e) { /* no body */ }
  if (!res.ok) {
    throw new Error((data && data.message) || 'Có lỗi xảy ra, vui lòng thử lại.');
  }
  return data;
}
