---
title: Mục tiêu và giới hạn dự trữ
---
<!-- l10n:target -->
**{k:QJ_MinQualityLabel}** yêu cầu lệnh sản xuất có số lần cố định tiếp tục thử cho đến khi tạo đủ vật phẩm đạt chất lượng đó trở lên. Vật phẩm dưới mục tiêu vẫn được giữ lại, nhưng không trừ vào số lần thực hiện còn lại của lệnh. Mỗi lần thử thêm đều tốn thêm nguyên liệu. **{k:QJ_AnyQuality}** chấp nhận mọi kết quả.

<!-- l10n:repeat-modes -->
Cơ chế thử lại này áp dụng cho **Làm X lần**. Với **Làm cho đến khi có X**, hãy dùng khoảng chất lượng của lệnh sản xuất thông thường để xác định vật phẩm nào được tính vào lượng dự trữ mục tiêu. **Làm mãi mãi** tiếp tục như thường lệ. Mục tiêu chất lượng không tự chọn người hoàn thiện giỏi hơn; bạn cũng cần đặt yêu cầu đối với người hoàn thiện.

<!-- l10n:stock -->
**Giới hạn dự trữ** giới hạn việc bắt đầu chế tạo thêm một loại sản phẩm trên bản đồ này. Các vật phẩm chưa hoàn thành của sản phẩm đó được tính chung trên mọi lệnh và bàn chế tạo, nhưng chỉ các lệnh được quản lý mới tuân theo giới hạn. **0 nghĩa là không giới hạn.** Vật phẩm đã hoàn thành trong kho không được tính.

<!-- l10n:cap-behavior -->
Khi đạt giới hạn, các lệnh được quản lý sẽ ngừng bắt đầu vật phẩm mới. Vẫn có thể tiếp tục hoặc hoàn thiện vật phẩm hiện có để xử lý hết hàng đợi. Nếu không ai đủ điều kiện hoàn thiện, hãy giảm yêu cầu hoặc bố trí lại người đủ điều kiện để hàng đợi có thể tiếp tục.

