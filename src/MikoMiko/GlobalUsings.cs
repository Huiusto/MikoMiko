// 全局类型别名：解决 WPF 与 WinForms 的同名类型冲突
// （工程启用了 UseWindowsForms 以使用托盘图标，因此必须显式消歧）
global using Application = System.Windows.Application;
global using UserControl = System.Windows.Controls.UserControl;
global using MessageBox = System.Windows.MessageBox;
global using Brush = System.Windows.Media.Brush;
global using Color = System.Windows.Media.Color;
global using Size = System.Windows.Size;
global using Point = System.Windows.Point;
global using Rect = System.Windows.Rect;
global using Timer = System.Threading.Timer;
global using Image = System.Windows.Controls.Image;
global using FontFamily = System.Windows.Media.FontFamily;
global using Clipboard = System.Windows.Clipboard;
