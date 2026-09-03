-- Tạo cơ sở dữ liệu
DROP DATABASE IF EXISTS xeghep_db;
CREATE DATABASE xeghep_db CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
USE xeghep_db;

-- 1. Bảng Người dùng chung
CREATE TABLE users (
    user_id INT AUTO_INCREMENT PRIMARY KEY,
    full_name VARCHAR(100) NOT NULL,
    phone VARCHAR(20) UNIQUE NOT NULL,
    email VARCHAR(100) UNIQUE DEFAULT NULL,
    password_hash VARCHAR(255) NOT NULL,
    role ENUM('admin', 'driver', 'passenger') NOT NULL,
    status ENUM('pending', 'active', 'locked') DEFAULT 'active',
    avatar VARCHAR(255) DEFAULT NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- 1.1 Bảng phiên đăng nhập
CREATE TABLE user_sessions (
    session_id INT AUTO_INCREMENT PRIMARY KEY,
    user_id INT NOT NULL,
    token VARCHAR(255) NOT NULL,
    ip_address VARCHAR(45) DEFAULT NULL,
    user_agent VARCHAR(255) DEFAULT NULL,
    expires_at DATETIME NOT NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (user_id) REFERENCES users(user_id) ON DELETE CASCADE
);

-- 1.2 Bảng hồ sơ hành khách
CREATE TABLE passenger_profiles (
    passenger_id INT PRIMARY KEY,
    identity_card VARCHAR(20) DEFAULT NULL,
    address VARCHAR(255) DEFAULT NULL,
    total_bookings INT DEFAULT 0,
    FOREIGN KEY (passenger_id) REFERENCES users(user_id) ON DELETE CASCADE
);

-- 2. Bảng thông tin chi tiết xe và hồ sơ tài xế
CREATE TABLE driver_profiles (
    driver_id INT PRIMARY KEY,
    vehicle_type VARCHAR(100) NOT NULL,
    license_plate VARCHAR(20) NOT NULL,
    rating DECIMAL(3,2) DEFAULT 5.00,
    total_trips INT DEFAULT 0,
    wallet_balance DECIMAL(12,2) DEFAULT 0.00,
    FOREIGN KEY (driver_id) REFERENCES users(user_id) ON DELETE CASCADE
);

-- 2.1 Bảng thống kê tỷ lệ nhận/từ chối chuyến của tài xế theo ngày và tháng (đáp ứng so sánh tuần/tháng)
CREATE TABLE driver_acceptance_stats (
    stat_id INT AUTO_INCREMENT PRIMARY KEY,
    driver_id INT NOT NULL,
    period_type ENUM('day', 'week', 'month') NOT NULL,
    period_value VARCHAR(20) NOT NULL, -- VD: '2026-08-18' cho ngày, '2026-W33' cho tuần, '2026-08' cho tháng
    accepted_count INT DEFAULT 0,      -- Số chuyến đã nhận
    rejected_count INT DEFAULT 0,      -- Số chuyến đã từ chối
    acceptance_rate DECIMAL(5,2) DEFAULT 0.00, -- Tỷ lệ chấp nhận (%)
    prev_period_rate DECIMAL(5,2) DEFAULT 0.00, -- Tỷ lệ của kỳ trước (để so sánh tăng/giảm)
    FOREIGN KEY (driver_id) REFERENCES users(user_id) ON DELETE CASCADE
);

-- 3. Bảng quản lý giấy tờ tài xế
CREATE TABLE driver_documents (
    doc_id INT AUTO_INCREMENT PRIMARY KEY,
    driver_id INT NOT NULL,
    doc_type VARCHAR(50) NOT NULL,
    doc_name VARCHAR(100) NOT NULL,
    file_path VARCHAR(255) NOT NULL,
    status ENUM('pending', 'approved', 'rejected') DEFAULT 'pending',
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (driver_id) REFERENCES users(user_id) ON DELETE CASCADE
);

-- 4. Bảng Chuyến đi (Hỗ trợ hiển thị chuyến sắp khởi hành)
CREATE TABLE trips (
    trip_id VARCHAR(20) PRIMARY KEY,
    driver_id INT NOT NULL,
    route_from VARCHAR(100) NOT NULL,
    route_to VARCHAR(100) NOT NULL,
    pickup_location VARCHAR(255) NOT NULL,
    dropoff_location VARCHAR(255) NOT NULL,
    departure_time DATETIME NOT NULL,
    price_per_seat DECIMAL(10,2) NOT NULL,
    total_seats INT NOT NULL,
    available_seats INT NOT NULL,
    status ENUM('upcoming', 'running', 'done', 'cancelled') DEFAULT 'upcoming',
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (driver_id) REFERENCES users(user_id) ON DELETE CASCADE
);

-- 5. Bảng Yêu cầu đặt chỗ / Đặt vé của hành khách
CREATE TABLE bookings (
    booking_id VARCHAR(20) PRIMARY KEY,
    trip_id VARCHAR(20) NOT NULL,
    passenger_id INT NOT NULL,
    seats INT NOT NULL DEFAULT 1,
    total_amount DECIMAL(10,2) NOT NULL,
    payment_method ENUM('online', 'cash') NOT NULL,
    payment_status ENUM('pending', 'paid', 'pending_cash', 'refunded') DEFAULT 'pending',
    status ENUM('pending_approval', 'approved', 'rejected', 'running', 'done', 'cancelled') DEFAULT 'pending_approval',
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (trip_id) REFERENCES trips(trip_id) ON DELETE CASCADE,
    FOREIGN KEY (passenger_id) REFERENCES users(user_id) ON DELETE CASCADE
);

-- 6. Bảng Giao dịch, Hoa hồng & Thu nhập cá nhân tài xế theo từng chuyến hoàn thành
CREATE TABLE transactions (
    transaction_id INT AUTO_INCREMENT PRIMARY KEY,
    booking_id VARCHAR(20) DEFAULT NULL,
    trip_id VARCHAR(20) DEFAULT NULL,
    passenger_id INT DEFAULT NULL,
    driver_id INT DEFAULT NULL,
    total_amount DECIMAL(10,2) NOT NULL,      -- Doanh thu chuyến
    commission_amount DECIMAL(10,2) NOT NULL, -- Hoa hồng nền tảng (VD: 10%)[cite: 1]
    driver_receive DECIMAL(10,2) NOT NULL,    -- Thu nhập cá nhân thực nhận của tài xế[cite: 1]
    payment_method ENUM('online', 'cash') NOT NULL,
    status ENUM('approved', 'pending_cash_audit', 'cancelled', 'refunded') DEFAULT 'approved',
    note VARCHAR(255) DEFAULT NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (booking_id) REFERENCES bookings(booking_id) ON DELETE SET NULL,
    FOREIGN KEY (trip_id) REFERENCES trips(trip_id) ON DELETE SET NULL,
    FOREIGN KEY (passenger_id) REFERENCES users(user_id) ON DELETE SET NULL,
    FOREIGN KEY (driver_id) REFERENCES users(user_id) ON DELETE SET NULL
);

-- 6.1 Bảng Đánh giá của khách hàng (Reviews)
CREATE TABLE reviews (
    review_id INT AUTO_INCREMENT PRIMARY KEY,
    trip_id VARCHAR(20) NOT NULL,
    passenger_id INT NOT NULL,
    driver_id INT NOT NULL,
    rating INT CHECK (rating BETWEEN 1 AND 5), -- Số sao đánh giá (1-5★)
    comment TEXT DEFAULT NULL,                -- Nhận xét của hành khách
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (trip_id) REFERENCES trips(trip_id) ON DELETE CASCADE,
    FOREIGN KEY (passenger_id) REFERENCES users(user_id) ON DELETE CASCADE,
    FOREIGN KEY (driver_id) REFERENCES users(user_id) ON DELETE CASCADE
);

-- 7. Bảng Yêu cầu rút tiền của tài xế
CREATE TABLE withdrawals (
    withdrawal_id VARCHAR(20) PRIMARY KEY,
    driver_id INT NOT NULL,
    amount DECIMAL(10,2) NOT NULL,
    bank_info VARCHAR(255) NOT NULL,
    status ENUM('pending', 'approved', 'rejected') DEFAULT 'pending',
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (driver_id) REFERENCES users(user_id) ON DELETE CASCADE
);

-- 8. Bảng Cài đặt hệ thống
CREATE TABLE system_settings (
    setting_key VARCHAR(50) PRIMARY KEY,
    setting_value VARCHAR(255) NOT NULL,
    description VARCHAR(255) DEFAULT NULL
);

INSERT INTO system_settings (setting_key, setting_value, description) VALUES
('commission_rate', '10', 'Tỉ lệ hoa hồng áp dụng cho mọi chuyến (%)[cite: 1]'),
('auto_payout', 'true', 'Tự động cộng tiền vào ví tài xế khi thanh toán online[cite: 1]');

-- 9. Bảng Phương thức thanh toán của hành khách
CREATE TABLE passenger_payment_methods (
    method_id INT AUTO_INCREMENT PRIMARY KEY,
    passenger_id INT NOT NULL,
    provider VARCHAR(50) NOT NULL,
    account_number VARCHAR(100) NOT NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (passenger_id) REFERENCES users(user_id) ON DELETE CASCADE
);

-- 10. Bảng Phân quyền (RBAC)
CREATE TABLE permissions (
    permission_id INT AUTO_INCREMENT PRIMARY KEY,
    permission_code VARCHAR(100) UNIQUE NOT NULL,
    description VARCHAR(255) DEFAULT NULL
);

CREATE TABLE role_permissions (
    role ENUM('admin', 'driver', 'passenger') NOT NULL,
    permission_id INT NOT NULL,
    PRIMARY KEY (role, permission_id),
    FOREIGN KEY (permission_id) REFERENCES permissions(permission_id) ON DELETE CASCADE
);

INSERT INTO permissions (permission_code, description) VALUES
('manage_system', 'Quản lý cấu hình, hoa hồng và toàn hệ thống'),
('manage_users', 'Khoá/mở khoá và quản lý tài khoản người dùng'),
('manage_drivers', 'Duyệt hồ sơ, xem giấy tờ và quản lý tài xế'),
('view_all_trips', 'Theo dõi toàn bộ chuyến đi trên hệ thống'),
('manage_own_trips', 'Đăng ký và quản lý chuyến đi của tài xế'),
('accept_booking', 'Chấp nhận hoặc từ chối yêu cầu đặt chỗ của khách'),
('book_trip', 'Tìm kiếm và đặt chỗ chuyến xe ghép'),
('withdraw_money', 'Tạo yêu cầu rút tiền về tài khoản ngân hàng');

INSERT INTO role_permissions (role, permission_id) 
SELECT 'admin', permission_id FROM permissions;

INSERT INTO role_permissions (role, permission_id) 
SELECT 'driver', permission_id FROM permissions 
WHERE permission_code IN ('manage_own_trips', 'accept_booking', 'withdraw_money');

INSERT INTO role_permissions (role, permission_id) 
SELECT 'passenger', permission_id FROM permissions 
WHERE permission_code IN ('book_trip');

-- ==========================================================
-- 11. CHÈN DỮ LIỆU MẪU ĐẦY ĐỦ
-- ==========================================================

-- Người dùng
INSERT INTO users (user_id, full_name, phone, email, password_hash, role, status, avatar) VALUES
(1, 'Admin Quản trị', '0900000000', 'quantri@xeghep.vn', '$2y$10$abcdef...', 'admin', 'active', 'A'),
(2, 'Trần Văn Hùng', '0912345678', 'hung.tran@email.com', '$2y$10$abcdef...', 'driver', 'active', 'H'),
(3, 'Phạm Đức Long', '0973451122', 'long.pham@email.com', '$2y$10$abcdef...', 'driver', 'active', 'L'),
(4, 'Nguyễn Văn Tùng', '0901122345', 'tung.nguyen@email.com', '$2y$10$abcdef...', 'driver', 'pending', 'T'),
(5, 'Nguyễn Thị Lan', '0918882233', 'lan.nguyen@email.com', '$2y$10$abcdef...', 'passenger', 'active', 'L'),
(6, 'Lê Quốc Anh', '0907714432', 'anh.le@email.com', '$2y$10$abcdef...', 'passenger', 'active', 'A'),
(7, 'Vũ Minh Đức', '0982239981', 'duc.vu@email.com', '$2y$10$abcdef...', 'passenger', 'locked', 'Đ');

-- Phiên đăng nhập
INSERT INTO user_sessions (user_id, token, ip_address, user_agent, expires_at) VALUES
(1, 'sess_token_admin_xyz123', '127.0.0.1', 'Mozilla/5.0 Chrome', '2026-08-25 23:59:59'),
(2, 'sess_token_driver_hung789', '127.0.0.1', 'Mozilla/5.0 Chrome', '2026-08-25 23:59:59'),
(5, 'sess_token_passenger_abc456', '127.0.0.1', 'Mozilla/5.0 Safari', '2026-08-25 23:59:59');

-- Hồ sơ hành khách
INSERT INTO passenger_profiles (passenger_id, identity_card, address, total_bookings) VALUES
(5, '001198001234', 'Hà Nội', 47),
(6, '001198005678', 'Hải Phòng', 112),
(7, '001198009999', 'Ninh Bình', 9);

-- Hồ sơ tài xế
INSERT INTO driver_profiles (driver_id, vehicle_type, license_plate, rating, total_trips, wallet_balance) VALUES
(2, 'Kia Carnival · 7 chỗ', '30F-689.21', 4.90, 312, 1850000.00),
(3, 'Toyota Innova · 7 chỗ', '29A-334.09', 4.70, 198, 500000.00),
(4, 'Toyota Innova · 4 chỗ', '30G-112.45', 5.00, 0, 0.00);

-- Thống kê tỷ lệ nhận/từ chối chuyến (trong ngày, trong tuần, so sánh tháng này với tháng trước)[cite: 2]
INSERT INTO driver_acceptance_stats (driver_id, period_type, period_value, accepted_count, rejected_count, acceptance_rate, prev_period_rate) VALUES
(2, 'day', '2026-08-18', 2, 1, 66.67, 75.00),     -- Thống kê ngày 18/08[cite: 2]
(2, 'week', '2026-W33', 14, 2, 87.50, 82.00),     -- Thống kê tuần
(2, 'month', '2026-08', 34, 6, 85.00, 88.00);     -- Thống kê tháng này so với tháng trước[cite: 2]

-- Giấy tờ tài xế
INSERT INTO driver_documents (driver_id, doc_type, doc_name, file_path, status) VALUES
(2, 'cccd', 'Căn cước công dân', '/uploads/docs/hung_cccd.jpg', 'approved'),
(2, 'license', 'Giấy phép lái xe', '/uploads/docs/hung_gplx.jpg', 'approved'),
(2, 'registration', 'Đăng kiểm xe', '/uploads/docs/hung_dangkiem.jpg', 'approved'),
(2, 'insurance', 'Bảo hiểm xe (mới)', '/uploads/docs/hung_baohiem.jpg', 'pending'),
(4, 'cccd', 'Căn cước công dân', '/uploads/docs/tung_cccd.jpg', 'pending');

-- Chuyến đi (Bao gồm chuyến sắp khởi hành)[cite: 2]
INSERT INTO trips (trip_id, driver_id, route_from, route_to, pickup_location, dropoff_location, departure_time, price_per_seat, total_seats, available_seats, status) VALUES
('TRIP-001', 2, 'Hà Nội', 'Ninh Bình', 'Bến xe Mỹ Đình', 'Trung tâm TP', '2026-08-22 06:30:00', 85000.00, 3, 0, 'upcoming'), -- Chuyến sắp khởi hành[cite: 2]
('TRIP-002', 3, 'Hà Nội', 'Hải Phòng', 'Cầu Giấy', 'Lạch Tray', '2026-08-19 14:02:00', 130000.00, 6, 5, 'running'),
('TRIP-003', 2, 'Hà Nội', 'Hải Phòng', 'Hà Nội', 'Hải Phòng', '2026-08-09 14:00:00', 120000.00, 4, 3, 'done'), -- Chuyến đã hoàn thành
('TRIP-004', 3, 'Hà Nội', 'Ninh Bình', 'Cầu Giấy', 'Tam Cốc', '2026-08-01 08:00:00', 90000.00, 4, 4, 'cancelled');

-- Đặt chỗ / Bookings
INSERT INTO bookings (booking_id, trip_id, passenger_id, seats, total_amount, payment_method, payment_status, status) VALUES
('BK-001', 'TRIP-001', 5, 2, 170000.00, 'online', 'paid', 'approved'),
('BK-002', 'TRIP-002', 6, 1, 130000.00, 'cash', 'pending_cash', 'running'),
('BK-003', 'TRIP-003', 5, 1, 120000.00, 'online', 'paid', 'done'),
('BK-004', 'TRIP-004', 5, 0, 0.00, 'online', 'refunded', 'cancelled');

-- Giao dịch & Thu nhập cá nhân của tài xế theo từng chuyến hoàn thành[cite: 1]
INSERT INTO transactions (booking_id, trip_id, passenger_id, driver_id, total_amount, commission_amount, driver_receive, payment_method, status, note) VALUES
('BK-001', 'TRIP-001', 5, 2, 120000.00, 12000.00, 108000.00, 'online', 'approved', 'Chuyến TRIP-001 thanh toán online — cộng ví tài xế'),
(NULL, 'TRIP-002', 6, 3, 130000.00, 13000.00, 117000.00, 'cash', 'pending_cash_audit', 'Chờ đối soát tiền mặt'),
('BK-003', 'TRIP-003', 5, 2, 390000.00, 39000.00, 351000.00, 'online', 'approved', 'Thu nhập chuyến hoàn thành TRIP-003');

-- Đánh giá của khách hàng (Reviews)[cite: 2]
INSERT INTO reviews (trip_id, passenger_id, driver_id, rating, comment) VALUES
('TRIP-003', 5, 2, 5, 'Tài xế lái xe rất cẩn thận và lịch sự, xe sạch sẽ!'),
('TRIP-002', 6, 3, 5, 'Nhận đánh giá 5 sao từ Quốc Anh[cite: 2]. Chuyến đi tuyệt vời.');

-- Yêu cầu rút tiền
INSERT INTO withdrawals (withdrawal_id, driver_id, amount, bank_info, status) VALUES
('WD-401', 2, 1000000.00, 'Vietcombank ****4821', 'pending'),
('WD-402', 3, 650000.00, 'MB Bank ****7710', 'pending');

-- Phương thức thanh toán của hành khách
INSERT INTO passenger_payment_methods (passenger_id, provider, account_number) VALUES
(5, 'Vietcombank', '****4821'),
(5, 'Ví Momo', '091 888 2233');