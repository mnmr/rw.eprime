---
title: Tùy chọn sản xuất
---
<!-- l10n:b-bdd2b459773e -->
Bật **Tự thêm công việc chế tạo bộ phận cấy ghép còn thiếu** để Implanner đặt công việc tại các bàn có thể chế tạo chúng và xóa công việc của mình khi hết nhu cầu. Các công việc bạn tự tạo không bao giờ bị xóa. Mỗi công việc chế tạo một lần theo thứ tự lắp khi phẫu thuật: với Trọn bộ, chân của một cư dân được làm trước tay nếu chân xếp cao hơn trong bảng xếp hạng cấy ghép dùng chung của bạn.

<!-- l10n:b-8effc9e9b828 -->
Khi nhiều công thức làm cùng cấy ghép, Implanner ưu tiên cách hiện đủ nguyên liệu. Có thể nâng cấp bộ phận cơ bản còn trống; nếu không, công thức trực tiếp làm loại nâng cao từ nguyên liệu. Nếu chỉ có cách nâng cấp, sản xuất trung gian có thể làm bản cơ bản trước. Công thức phải được nghiên cứu và có bàn đủ điều kiện.

<!-- l10n:b-0aa3ded1d655 -->
Để dành riêng một bàn cho việc chế tạo bộ phận cấy ghép, hãy chọn **Bàn Implanner** ở thẻ Công việc của bàn. Ô chọn chỉ xuất hiện tại các bàn chế tạo được bộ phận cấy ghép. Các công việc sẵn có trên bàn sẽ bị tạm dừng, và Implanner đặt công việc chế tạo của mình vào bàn này trước mọi bàn khác.

<!-- l10n:queue -->
Khi ô này được chọn, thẻ Công việc hiển thị hàng đợi sản xuất của Implanner thay cho danh sách công việc thông thường của bàn. Mỗi hàng là một công việc do Implanner xếp vào hàng đợi: vật phẩm, việc đã có người chế tạo bắt đầu làm hay chưa, kế hoạch mà công việc thuộc về, cư dân kế tiếp được giao công việc và thời gian công việc đã nằm trong hàng đợi. Công việc cho một nguyên liệu sẽ ghi tên bộ phận cấy ghép cần đến nó. **Chi tiết** mở phần cài đặt của game cho công việc đó. Dòng ở cuối cho biết số công việc do bạn tạo đang bị tạm dừng. Bạn không thể thêm công việc vào bàn cho đến khi bỏ chọn.

<!-- l10n:b-a4d04c1bceb2 -->
![Bàn gia công được giao cho Implanner](implanner-bench.png)

<!-- l10n:b-a89a880dcac5 -->
Bỏ chọn để lấy lại bàn. Công việc đã tạm dừng trước lúc bật vẫn dừng; các công việc khác tiếp tục. Nếu gỡ Implanner khi bàn đang được chọn, bàn vẫn dùng được nhưng bạn phải tự tiếp tục công việc.

<!-- l10n:b-c9f0dad1a5c5 -->
![Các tùy chọn sản xuất](production-options.png)

<!-- l10n:b-f6f358878993 -->
- **Số bàn được nhận công việc** giới hạn số bàn cùng làm cấy ghép mỗi khu định cư. Mỗi bàn giữ tối đa hai công việc Implanner để có việc tiếp ngay. Bàn Implanner cũng được tính.
- **Chỉ tạo công việc tại bàn rảnh** tránh bàn đã có việc đang chờ, giữ ưu tiên hàng đợi của bạn.
- **Chỉ dùng bàn Implanner** giới hạn công việc ở bàn Implanner; không có bàn thì không chế tạo. Khi tắt, bàn khác được dùng sau khi mọi bàn Implanner có hai công việc.
- **Kỹ năng chế tạo tối thiểu cho công việc** tránh giao vật liệu đắt cho thợ yếu.
- **Cho phép chế tạo nguyên liệu trung gian còn thiếu** theo chuỗi thiếu hụt: thiếu một linh kiện cao cấp cho chân sinh học thì làm linh kiện trước. Khi cấy ghép chờ tài nguyên thô như plasteel, bàn rảnh làm linh kiện cao cấp sẽ cần. Sản phẩm chế tạo như linh kiện và cấy ghép cơ bản hợp lệ. Không chế tạo tài nguyên thô như thép dù có công thức luyện kim tạo ra; bạn vẫn phải thu thập.

<!-- l10n:b-24089426e9a9 -->
Mọi công việc đều tôn trọng mức giữ kho (xem [giữ dự trữ](topic:keeping-stock)).
