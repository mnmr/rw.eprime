---
title: Chọn người hoàn thiện
---
<!-- l10n:requirements -->
Dùng các tùy chọn này trên lệnh sản xuất hoặc công trình. Những người lao động đủ điều kiện khác có thể làm các công đoạn trước; chỉ công đoạn hoàn thiện cuối cùng mới phải đáp ứng yêu cầu của bạn.

<!-- l10n:controls -->
- **Kỹ năng người hoàn thiện** đặt cấp kỹ năng tối thiểu.
- **{k:QJ_AutoBest}** chọn lao động theo chất lượng kỳ vọng thay vì ngưỡng kỹ năng tối thiểu bạn đặt thủ công. Giá trị thanh trượt thủ công được giữ lại để dùng khi bạn tắt tùy chọn này.
- **{k:QJ_RequireInspired}** chờ cảm hứng đó. Hoàn tất công việc sẽ tiêu hao cảm hứng như bình thường.
- **{k:QJ_RequireSpecialist}** yêu cầu một vai trò trong Ideology có mức cộng chất lượng sản xuất. Tùy chọn này xuất hiện khi có Ideology.

<!-- l10n:odds-image -->
![Yêu cầu đối với người hoàn thiện và bản xem trước xác suất chất lượng](finisher-options.png)

<!-- l10n:best -->
Lựa chọn tự động so sánh cư dân đủ điều kiện, mech của thuộc địa và robot được hỗ trợ, rồi chọn lao động có chất lượng kỳ vọng cao nhất. Kỹ năng, cảm hứng sáng tạo, các mức cộng từ chuyên gia sản xuất và mức cộng chất lượng của mod được hỗ trợ đều được tính đến. Cách này tối đa hóa chất lượng trung bình, nhưng không bảo đảm kết quả của từng lần xác định chất lượng ngẫu nhiên.

<!-- l10n:availability -->
Lao động đáp ứng các điều kiện khác vẫn được đưa vào so sánh khi vắng mặt, gục ngã, ngủ, suy sụp tinh thần hoặc đang sạc, kể cả robot nằm trong trạm. Nếu không ai trong số lao động đứng đầu có thể hoàn thiện trên bản đồ này, công việc sẽ chờ. Ví dụ, một thợ chế tác giỏi hơn đang đi cùng đoàn lữ hành có thể khiến lao động xếp hạng thấp hơn ở thuộc địa không được hoàn thiện. Không có thời hạn chờ nào làm giảm yêu cầu chất lượng.

<!-- l10n:manual-threshold -->
Khi tắt **{k:QJ_AutoBest}**, bất kỳ lao động nào có khả năng thực hiện công việc và đáp ứng ngưỡng kỹ năng bạn đặt thủ công cùng các yêu cầu khác đều có thể hoàn thiện. Lao động giỏi hơn ở nơi khác trong thuộc địa không ngăn cản họ.

<!-- l10n:robot-finishers -->
Mech và robot được hỗ trợ phải đáp ứng các yêu cầu hoàn thiện giống như cư dân. Yêu cầu cảm hứng sáng tạo hoặc vai trò chuyên gia sản xuất vẫn loại trừ những lao động không có cảm hứng hoặc vai trò tương ứng. Tắt **{k:QJ_SettingsAllowRobotPreparation}** sẽ dành các công đoạn trước cho cư dân; mech và robot đủ điều kiện vẫn có thể nhận công việc hoàn thiện của Quality Jobs.

<!-- l10n:odds -->
Trong bảng **{k:QJ_OddsHeader}** của lệnh sản xuất, **{k:QJ_OddsColConfig}** ước tính xác suất dựa trên cấp kỹ năng hoàn thiện tối thiểu và các mức cộng mà bạn yêu cầu. Khi bật **{k:QJ_AutoBest}** và tìm được ứng viên đủ điều kiện, cột này sẽ hiển thị xác suất của lao động được chọn từ toàn thuộc địa. Nếu không có ứng viên, cột này dùng lại thiết lập thủ công của bạn; điều đó không có nghĩa là có người hoàn thiện sẵn sàng.

<!-- l10n:odds-best -->
**{k:QJ_OddsColBest}** hiển thị xác suất của lao động có khả năng thực hiện công việc được xếp hạng cao nhất trên bản đồ này, bỏ qua các yêu cầu hoàn thiện bổ sung của bạn. Vì vậy, hai cột có thể mô tả hai lao động khác nhau. Đây là xác suất, không phải bảo đảm; lao động thực sự hoàn thiện công việc mới quyết định kết quả.

<!-- l10n:eligible-finishers -->
**Người hoàn thiện đủ điều kiện** liệt kê những lao động có thể đáp ứng yêu cầu trên bản đồ này ngay lúc này; những lao động vắng mặt, gục ngã hoặc suy sụp tinh thần không được đưa vào danh sách. Với lệnh sản xuất, danh sách áp dụng các yêu cầu của lệnh đang chọn, dù số lượng trong hàng đợi bao gồm cả các lệnh khác làm cùng sản phẩm trên bản đồ này.

<!-- l10n:priority -->
**{k:QJ_SettingsHighPriorityFinish}** khiến công việc hoàn thiện được giao được ưu tiên trước các việc khác, trừ chữa cháy, chăm sóc y tế, cứu hộ và nghỉ dưỡng trên giường. Tùy chọn này không ngắt công việc đang làm. Khi tắt, công việc hoàn thiện được ưu tiên trong cùng loại công việc, sau các loại công việc có mức ưu tiên cao hơn. Lao động phải có khả năng thực hiện loại công việc thông thường của vật phẩm.

