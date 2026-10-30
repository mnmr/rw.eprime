---
title: Thiết lập lệnh sản xuất
---
<!-- l10n:open -->
Mở **Chi tiết** trên lệnh sản xuất vật phẩm có cấp chất lượng. Bảng **{k:QJ_QualityPanelTitle}** xuất hiện cạnh các tùy chọn thông thường của lệnh đối với công thức được hỗ trợ có tạo ra vật phẩm dở dang. Các công thức không có cấp chất lượng hoặc công đoạn dở dang vẫn hoạt động như bình thường.

<!-- l10n:bill-image -->
![Lệnh sản xuất tại bàn chế tạo cùng bảng Quality Jobs](crafting-bill.png)

<!-- l10n:setup -->
Bật **{k:QJ_ManageBill}**, rồi chọn [yêu cầu đối với người hoàn thiện](topic:finishers). Người lao động thông thường có thể làm phần lớn công việc chế tạo. Nếu người đang làm đáp ứng yêu cầu, họ sẽ hoàn thành ngay; nếu không, vật phẩm gần hoàn thành sẽ được để riêng, và lệnh có thể bắt đầu chế tạo thêm vật phẩm trong giới hạn tồn kho.

<!-- l10n:dispatch -->
Khi có người hoàn thiện đáp ứng yêu cầu, Quality Jobs tạo một lệnh **Hoàn thiện …** tạm thời và giao cho họ. Họ lấy vật phẩm dở dang rồi hoàn thành nó. Lệnh tạm thời sẽ tự xóa sau đó. Vật phẩm đang chờ có thể được vận chuyển và cất giữ như các vật phẩm dở dang khác.

<!-- l10n:finisher-bill-restrictions -->
Giới hạn kỹ năng tối đa và chỉ định người làm cụ thể của lệnh sản xuất gốc áp dụng cho đến khi vật phẩm được để riêng chờ hoàn thiện. Bạn có thể giao công việc cho người đang học nghề, còn cư dân có kỹ năng cao hơn, mech của thuộc địa hoặc robot được hỗ trợ sẽ nhận lệnh hoàn thiện riêng. Người hoàn thiện vẫn phải có khả năng thực hiện công thức và đáp ứng các yêu cầu của Quality Jobs.

<!-- l10n:status -->
Mục **{k:QJ_StatusHeader}** đếm các vật phẩm dở dang của sản phẩm này đang được theo dõi trên mọi lệnh sản xuất và bàn làm việc trên bản đồ này: công việc **Dở dang** được chia sẻ, vật phẩm **Chờ** người hoàn thiện và vật phẩm đã được giao để **Hoàn thiện**. Danh sách người hoàn thiện và cảnh báo dựa trên yêu cầu của lệnh đang chọn; các lệnh khác làm cùng sản phẩm có thể có yêu cầu khác. Di chuột lên trạng thái và danh sách người hoàn thiện để xem chi tiết. Xem [khi công việc phải chờ](topic:waiting) nếu không có tiến triển.

<!-- l10n:disable -->
Bỏ chọn **{k:QJ_ManageBill}** để lệnh được hoàn thành bình thường trở lại. Khi bật chia sẻ, những người lao động khác vẫn có thể tiếp tục phần việc được chia sẻ của lệnh.

