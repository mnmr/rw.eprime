---
title: Cách tự động hóa hoạt động
---
<!-- l10n:b-44e89be1e85f -->
Tự động hóa kiểm tra khu định cư khoảng hai lần mỗi giờ trong trò chơi. Sau mỗi chỉnh sửa, hệ thống kiểm tra lại khi thời gian trong trò chơi tiếp tục trôi. Bỏ tạm dừng để áp dụng các thay đổi về tự động hóa. Mỗi lượt kiểm tra thực hiện các bước sau:

<!-- l10n:b-6eb19bb9f96f -->
1. Đếm phần cư dân được giao kế hoạch còn thiếu.
2. Xếp lệnh cho cấy ghép khu định cư chế tạo được (xem [tùy chọn sản xuất](topic:production-options)).
3. Giữ vật phẩm hoàn chỉnh trong kho cho từng cư dân theo ưu tiên và bậc.
4. Khi mọi thứ cho đợt tiếp theo đã được giữ và có tại chỗ, thêm các ca mổ thành một khối sau công việc đã xếp sẵn.

<!-- l10n:b-a747349bea45 -->
Tóm tắt khu định cư ở đầu thẻ Tổng quan cho biết tự động hóa có bật không, đang chế tạo hoặc chờ gì, số bộ phận cấy ghép trong kho và còn cần sản xuất, ai sắp được phẫu thuật. Trỏ vào số liệu sản xuất hoặc phẫu thuật để xem chi tiết từng loại cấy ghép, bao gồm từng mục đang chờ gì.

<!-- l10n:production-counts -->
Trong chú giải sản xuất, **Còn cần sản xuất** đếm số bộ phận cấy ghép cần thiết mà hàng trong kho chưa đáp ứng, kể cả phần chưa có lệnh chế tạo. **Trong lệnh chế tạo** đếm sản lượng dự kiến từ các lệnh Implanner hiện có, dù đang chờ hay đang thực hiện.

<!-- l10n:b-8fea0b171c65 -->
![Tóm tắt khu định cư với trạng thái sản xuất và phẫu thuật](colony-next.png)

<!-- l10n:b-26d1852e16fa -->
Mỗi cấy ghép vẫn cần một ca mổ riêng. Chia đợt hoãn mổ để lắp nhiều bộ phận trong một lần gây mê, giảm số lần gây mê riêng lẻ. Một đợt là mọi thứ cư dân cần tiếp theo: bậc hiện tại hoặc cả kế hoạch tùy trình tự (xem [tùy chọn phẫu thuật](topic:surgery-options)). Có trình tự bỏ chia đợt hoàn toàn, xếp lịch từng cấy ghép ngay khi được giữ riêng và có tại chỗ.

<!-- l10n:b-6b9f966652bc -->
Ca mổ chờ cư dân đủ khỏe. Các khu định cư không lấy đồ của nhau: giữ và lắp vật phẩm ở nơi cư dân sống.
