---
title: Bionic modularity
icon: verdict-blue.png
---
<!-- l10n:verdict -->
| **Kết luận** | ![Tương thích hoàn toàn](verdict-blue.png|16) Tương thích hoàn toàn (hỗ trợ riêng) |
| **Bằng chứng** | Phân tích mã nguồn, kiểm thử tự động trong game |
| **Đã kiểm tra** | 2026-09-05 |
| **Phiên bản** | Implanner 1.1.0, Bionic modularity [Workshop 3589154549](https://steamcommunity.com/sharedfiles/filedetails/?id=3589154549), tệp 2026-09-05 |

<!-- l10n:summary -->
Mô-đun dùng chung chi bionic với nó, và mô-đun chi sẽ yêu cầu vật chủ.

<!-- l10n:b-c7cd0a958354 -->
![Hộp thoại yêu cầu cho mô-đun chân](requirement-prompt.png)

<!-- l10n:b-71ab3bae9608 -->
![Kế hoạch sau khi Add to plan: chân bionic và mô-đun đều đã được chọn](requirement-added.png)

<!-- l10n:b-1a485abf9e8c -->
Mỗi mô-đun chiếm một ô trên bộ phận cơ thể của nó, nên một mô-đun chiến đấu và một mô-đun công việc có thể gắn cùng một cánh tay, còn hai mô-đun chiến đấu thì không. Mô-đun cho chi được lắp lên chi bionic và dùng chung bộ phận với nó, nên cánh tay bionic và mô-đun cánh tay cùng nằm trong kế hoạch được. Khi tích một mô-đun chi mà kế hoạch chưa có bionic ở bộ phận đó, một hộp thoại sẽ mở ra, liệt kê các bionic có thể làm vật chủ, mặc định chọn vật chủ kém tiên tiến nhất. **Add to plan** sẽ thêm lựa chọn của bạn cùng mô-đun. **Cancel** không thêm gì cả.

