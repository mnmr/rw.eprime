---
title: Tùy chọn sản xuất
---
Bật **Tự thêm lệnh chế tạo bộ phận cấy ghép còn thiếu** để Implanner đặt lệnh tại bàn làm được và xóa lệnh của nó khi hết nhu cầu. Không xóa lệnh bạn tạo. Mỗi lệnh chế tạo một lần theo thứ tự lắp: với Trọn bộ, chân trước tay nếu chân xếp cao hơn trong kế hoạch.

Khi nhiều công thức làm cùng cấy ghép, Implanner ưu tiên cách hiện đủ nguyên liệu. Có thể nâng cấp bộ phận cơ bản còn trống; nếu không, công thức trực tiếp làm loại nâng cao từ nguyên liệu. Nếu chỉ có cách nâng cấp, sản xuất trung gian có thể làm bản cơ bản trước. Công thức phải được nghiên cứu và có bàn đủ điều kiện.

Để dành bàn riêng, chọn **Bàn Implanner** ở thẻ Công việc của bàn. Ô chọn chỉ có tại bàn chế tạo được cấy ghép. Lệnh sẵn có bị tạm dừng; Implanner ưu tiên bàn này. Khi bật, không thể thêm, dán, tạm dừng hoặc tiếp tục lệnh trên bàn.

![Bàn gia công được giao cho Implanner](implanner-bench.png)

Bỏ chọn để lấy lại bàn. Lệnh đã tạm dừng trước lúc bật vẫn dừng; các lệnh khác tiếp tục. Nếu gỡ Implanner khi bàn đang được chọn, bàn vẫn dùng được nhưng bạn phải tự tiếp tục lệnh.

![Các tùy chọn sản xuất](production-options.png)

- **Số bàn được nhận lệnh** giới hạn số bàn cùng làm cấy ghép mỗi khu định cư. Mỗi bàn giữ tối đa hai lệnh Implanner để có việc tiếp ngay. Bàn Implanner cũng được tính.
- **Chỉ tạo lệnh tại bàn rảnh** tránh bàn đã có việc đang chờ, giữ ưu tiên hàng đợi của bạn.
- **Chỉ dùng bàn Implanner** giới hạn lệnh ở bàn Implanner; không có bàn thì không chế tạo. Khi tắt, bàn khác được dùng sau khi mọi bàn Implanner có hai lệnh.
- **Kỹ năng chế tạo tối thiểu cho lệnh** tránh giao vật liệu đắt cho thợ yếu.
- **Cho phép chế tạo nguyên liệu trung gian còn thiếu** theo chuỗi thiếu hụt: thiếu một linh kiện cao cấp cho chân sinh học thì làm linh kiện trước. Khi cấy ghép chờ tài nguyên thô như plasteel, bàn rảnh làm linh kiện cao cấp sẽ cần. Sản phẩm chế tạo như linh kiện và cấy ghép cơ bản hợp lệ. Không chế tạo tài nguyên thô như thép dù có công thức luyện kim tạo ra; bạn vẫn phải thu thập.

Mọi lệnh đều tôn trọng mức giữ kho (xem [giữ dự trữ](topic:keeping-stock)).
