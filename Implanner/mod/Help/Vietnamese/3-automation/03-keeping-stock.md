---
title: Giữ dự trữ
---
<!-- l10n:b-2d4a360fb110 -->
Hai thiết lập ngăn tự động hóa rút cạn kho.

<!-- l10n:b-f53aef22895d -->
**Giữ trong kho** đặt mức tối thiểu tài nguyên cho sản xuất. Chỉ thêm lệnh khi đủ chi phí, tính cả nguyên liệu lệnh đang chờ còn cần, mà vẫn giữ ít nhất lượng đã đặt của từng nguyên liệu. Đặt thép 2000 nghĩa là chế tạo cấy ghép không bao giờ kéo thép dưới 2000. Nếu bạn tiêu nguyên liệu làm lệnh chưa ai bắt đầu không còn đáp ứng, Implanner xóa nó rồi thêm lại khi kho đủ.

<!-- l10n:b-96bbe1392879 -->
![Mức tài nguyên giữ trong kho](keep-in-stock.png)

<!-- l10n:b-5864119cb6f1 -->
**Cấy ghép giữ riêng** giữ lại tối đa số lượng cấy ghép hiện có đã đặt để dùng thủ công. Giữ riêng một tay sinh học thì tự động hóa chỉ phân bổ từ cái thứ hai, để một cái dùng mổ khẩn cấp hoặc trao đổi.
Sản xuất tự động chế tạo thêm cấy ghép khi các kế hoạch đã gán còn cần chúng. Để bổ sung kho khẩn cấp khi không có kế hoạch nào cần cấy ghép, hãy tự thêm lệnh chế tạo.
Lệnh nâng cấp cũng không dùng cấy ghép giữ riêng hoặc cấy ghép cần cho ca mổ dự kiến. Chỉ cấy ghép dư mới được dùng làm nguyên liệu sản xuất.

<!-- l10n:b-04d4c3acbddc -->
![Giữ riêng một tay sinh học](implant-reservations.png)
