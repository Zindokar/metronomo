namespace MetronomoMovil;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

		// El número de BPM es un campo de texto sin borde ni subrayado.
		Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping("SinBorde", (handler, _) =>
		{
#if ANDROID
			handler.PlatformView.BackgroundTintList =
				Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent);
#elif IOS
			handler.PlatformView.BorderStyle = UIKit.UITextBorderStyle.None;
#endif
		});

		return builder.Build();
	}
}
