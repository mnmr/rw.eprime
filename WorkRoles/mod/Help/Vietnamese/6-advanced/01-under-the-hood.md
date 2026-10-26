---
title: Cơ chế bên trong
---
Vai trò có thứ tự của từng cư dân được biên dịch thành một thứ tự việc chặt chẽ, đưa vào trò chơi qua cùng danh sách mà cơ chế chọn việc gốc đọc. Hành vi tương đương bảng ưu tiên chỉnh thủ công, không cần bản vá mỗi tick hay xử lý mỗi khung hình.

Mọi thứ tính lại khi phân công hoặc vai trò đổi, rồi nằm trong bộ đệm. Vai trò có điều kiện còn cập nhật ở ranh giới giờ và khi đổi vị trí. Việc khẩn như chữa cháy dùng lượt việc khẩn cấp của trò chơi nếu một trong năm vai trò đầu đang bật chứa nó; phần còn lại chạy ở vị trí thường. Tắt "Chỉ 5 vai trò đầu ngắt việc khi khẩn cấp" để mọi việc khẩn được bao phủ dùng lượt đó.

WorkRoles giữ kho ưu tiên gốc đồng bộ từ vai trò (mặc định ánh xạ 0–4 khi tùy chọn phạm vi gốc bật), nên gỡ mod an toàn: thẻ Công việc gốc trở lại với ưu tiên chuyển từ vai trò. Thêm mod vào bản lưu cũ chuyển theo chiều ngược lại.
