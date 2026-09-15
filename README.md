# StairDrawer

Plugin C# cho AutoCAD 2021, target `.NET Framework 4.8` và `x64`.

## Chức năng

- `STAIRDRAW`: chọn vẽ mặt bằng hoặc mặt cắt.
- `STAIRPLAN`: vẽ mặt bằng thang thẳng, chữ L hoặc chữ U; phân nét thấy/khuất theo cao độ mặt phẳng cắt, đánh số bậc lẻ, ghi cao độ và tạo mũi tên hướng lên.
- `STAIRSECTION`: vẽ mặt cắt cấu tạo gồm bản thang BTCT, bậc xây gạch, lớp mặt/cổ bậc và cốt thép minh họa.
- Tất cả kích thước nhập theo đơn vị bản vẽ hiện hành; bộ giá trị mặc định được thiết kế cho bản vẽ dùng milimét.
- Hình học được đặt trên mặt phẳng WCS XY tại cao độ Z của điểm chèn.

Mặt bằng dùng bộ layer tham khảo từ bản vẽ mẫu:

- `ABC_A_NetThay`: đường bao và bậc nằm dưới mặt phẳng cắt.
- `ABC_A_Khuat`: phần nằm trên mặt phẳng cắt, dùng linetype `HIDDEN` và linetype scale 2.
- `ABC_A_Netcat`: ký hiệu zíc-zắc tại vị trí mặt phẳng cắt qua vế thang.
- `ABC_A_Anotation`: mũi tên hướng lên, polyline có global width 50.
- `ABC_A_Text`: số bậc, cao độ và ghi chú, text height 120.

Mặt cắt là cấu tạo triển khai sơ bộ. Kích thước bản thang, lớp bảo vệ và cốt thép phải được kỹ sư kết cấu kiểm tra trước khi dùng cho hồ sơ thi công.

## Build

Các Autodesk managed DLL không được copy vào output. Truyền thư mục cài AutoCAD khi build:

```powershell
dotnet build .\StairDrawer.sln -c Release -p:Platform=x64 -p:AutoCADManagedDir="C:\Program Files\Autodesk\AutoCAD 2021"
```

Hoặc đặt biến môi trường `ACAD_MANAGED_DIR`, sau đó build bình thường. Bundle được tạo tại `bin\x64\Release\net48\StairDrawer.bundle` và không tự động deploy vào `ApplicationPlugins`.

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

- Dùng `NETLOAD` và chọn `StairDrawer.dll`; hoặc
- Copy cả thư mục `StairDrawer.bundle` vào `%AppData%\Autodesk\ApplicationPlugins`, sau đó khởi động lại AutoCAD.
