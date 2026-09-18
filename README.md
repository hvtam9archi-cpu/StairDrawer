# StairDrawer

Plugin C# cho AutoCAD 2021, target `.NET Framework 4.8` và `x64`.

## Chức năng

- `STAIRDRAW`: mở giao diện WPF để chọn mặt bằng/mặt cắt, loại thang, xem preview và nhập toàn bộ thông số.
- Tất cả kích thước nhập theo đơn vị bản vẽ hiện hành; bộ giá trị mặc định được thiết kế cho bản vẽ dùng milimét.
- Hình học được đặt trên mặt phẳng WCS XY tại cao độ Z của điểm chèn.

Sau khi xác nhận giao diện, AutoCAD chỉ yêu cầu chọn điểm chèn và điểm xác định hướng trục X. Các thông số không còn được hỏi tuần tự tại con trỏ hoặc command line.

Mặt bằng dùng bộ layer tham khảo từ bản vẽ mẫu:

- `ABC_A_NetThay`: đường bao và bậc nằm dưới mặt phẳng cắt.
- `ABC_A_Khuat`: phần nằm trên mặt phẳng cắt, dùng linetype `HIDDEN` và linetype scale 2.
- `ABC_A_Netcat`: ký hiệu zíc-zắc tại vị trí mặt phẳng cắt qua vế thang.
- `ABC_A_Anotation`: đường chỉ hướng liên tục, đoạn xuất phát rộng 45 dài 150 và đầu mũi tên dạng chữ V theo bản vẽ tham khảo.
- `ABC_A_Text`: số bậc lẻ; chiều cao chữ tự điều chỉnh trong khoảng 100-180 theo bề rộng vế thang.

Mặt cắt là cấu tạo triển khai sơ bộ. Lớp hoàn thiện cổ bậc được đưa lên trên lớp mặt bậc theo thứ tự vẽ; mũi bậc nhô 20 mm so với mặt cổ bậc và có cạnh vát chéo. Kích thước và bản thang phải được kỹ sư kết cấu kiểm tra trước khi dùng cho hồ sơ thi công.

## Build

Các Autodesk managed DLL không được copy vào output. Truyền thư mục cài AutoCAD khi build:

```powershell
dotnet build .\StairDrawer.sln -c Release -p:Platform=x64 -p:AutoCADManagedDir="C:\Program Files\Autodesk\AutoCAD 2021"
```

Hoặc đặt biến môi trường `ACAD_MANAGED_DIR`, sau đó build bình thường. Mỗi lần build, bundle được tạo trong thư mục output và tự động deploy vào `%AppData%\Autodesk\ApplicationPlugins\StairDrawer.bundle`.

Có thể tắt deploy cho một lần build bằng `-p:DeployAutoCADBundle=false`, hoặc đổi thư mục đích bằng `-p:ApplicationPluginsDir="..."`.

### Debug trực tiếp bằng Visual Studio

Project áp dụng luồng debug của `gileCAD/AutoCAD-Csharp-Project-Template`: Visual Studio khởi động AutoCAD và truyền một script để `NETLOAD` đúng DLL Debug. Script được sinh trong output khi build nên không lưu đường dẫn máy developer vào source.

Đặt hai MSBuild property trong file `StairDrawer.csproj.user` cục bộ (file này đã được ignore):

```xml
<Project>
  <PropertyGroup>
    <AutoCADManagedDir>C:\Program Files\Autodesk\AutoCAD 2021</AutoCADManagedDir>
    <AutoCADExecutable>C:\Program Files\Autodesk\AutoCAD 2021\acad.exe</AutoCADExecutable>
  </PropertyGroup>
</Project>
```

Chọn cấu hình `Debug | x64` và nhấn `F5`. File `start.scr` tương ứng được tạo cạnh `StairDrawer.dll` trong thư mục output.

## Nạp plugin

- Sau khi build, khởi động lại AutoCAD để nạp bundle đã được deploy tự động; hoặc
- Dùng `NETLOAD` và chọn `StairDrawer.dll` trong thư mục output khi cần kiểm tra thủ công.
