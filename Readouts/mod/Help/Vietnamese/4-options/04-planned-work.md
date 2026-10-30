---
title: Việc dự kiến
---
<!-- l10n:b-7aa1a59c7379 -->
Các tùy chọn việc dự kiến trừ lượng vật liệu ước tính cần cho lệnh hoặc bản thiết kế khỏi số lượng hiển thị.

<!-- l10n:b-ee40b81e925d -->
- Dành tài nguyên cho lệnh trừ nguyên liệu cần cho các lệnh sản xuất chưa hoàn thành. Một nguyên liệu chỉ được tính khi lệnh cho phép dùng đúng một loại tài nguyên cho nguyên liệu đó. Ví dụ, cho phép dùng bất kỳ loại da nào sẽ không dành trước gì; chỉ cho phép một loại da sẽ tính lượng da đó cần dùng. "Làm X lần" dành X lượt; "Làm đến khi có X" dành số lượt cần đạt mục tiêu; "Làm mãi" dành một lượt. Lệnh tạm dừng không dành gì.
- Dành tài nguyên cho công trình trừ vật liệu bản thiết kế và khung dở còn cần. Vật liệu đã giao vào khung hoặc được cư dân nhặt lên để giao được tính là đã dùng, không trừ lần hai. Bỏ qua bản thiết kế bị cấm.
- Hiện thiếu hụt bằng số âm hiện lượng thiếu khi nhu cầu vượt kho, thay vì dừng ở 0.
- Hỗ trợ Quality Jobs điều chỉnh lượng tài nguyên dành cho lệnh và công trình theo mục tiêu chất lượng do Quality Jobs quản lý khi các tùy chọn dành tài nguyên tương ứng được bật. Tính năng này ước tính vật liệu cho các lần làm lại và tính đến vật liệu thu hồi từ công trình. Bật tùy chọn dành tài nguyên tương ứng ở trên để sử dụng. Hàng bị mờ khi chưa cài EPrime's Quality Jobs hoặc phiên bản quá cũ; trỏ để xem lý do.

<!-- l10n:planned-work-tooltip-columns -->
Trỏ vào tài nguyên hoặc tập tài nguyên có khấu trừ cho việc dự kiến để xem bảng Việc dự kiến. Công việc còn lại là số lượt thực hiện công thức hoặc số công trình cần xây. Chi phí mỗi lần là lượng vật liệu cần cho một lần thực hiện. Đã trừ là lượng bị trừ khỏi số lượng hiển thị, có tính đến vật liệu đã giao hoặc đang được vận chuyển đến công trình và số lần làm lại ước tính để đạt chất lượng khi bật hỗ trợ Quality Jobs.

<!-- l10n:b-a4ac1a2f64dc -->
Bật Hiện thiếu hụt bằng số âm để số bị việc dự kiến kéo dưới 0 hiện lượng thiếu màu cam:

<!-- l10n:b-b209bc9139c1 -->
![Vật liệu khi bật số âm](negatives-on.png)

<!-- l10n:b-5325cc201c17 -->
Tắt thì cùng các ô dừng ở 0:

<!-- l10n:b-a997f13279d8 -->
![Cùng các ô khi tắt số âm](negatives-off.png)

<!-- l10n:b-f017c3669f5c -->
Việc dự kiến được cập nhật cùng số lượng tài nguyên sau mỗi vài giây trong trò chơi. Lệnh mới hoặc đã thay đổi sẽ được phản ánh ở lần cập nhật tiếp theo; bỏ tạm dừng để thời gian trò chơi tiếp tục trôi.
