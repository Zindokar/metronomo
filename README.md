# Metrónomo

Metrónomo de tempo y subdivisiones ajustables para **Windows**, **Android** e **iOS**.

- Tempo de 20 a 400 BPM (botones −/+, deslizador o escribiendo el número).
- De 1 a 16 tiempos por compás, con el primero acentuado (opcional).
- Subdivisiones: negras, corcheas, tresillos, semicorcheas, quintillos, seisillos, septillos y fusas.
- Volumen general y volumen de la subdivisión por separado.
- Indicador visual sincronizado con lo que realmente suena.

## Precisión

Los clics se generan **dentro del callback de audio**, contando muestras, en lugar de usar
temporizadores. El tempo es exacto a nivel de muestra (≈0,02 ms a 48 kHz) y no se ve afectado
por la carga del sistema ni por la interfaz.

## Estructura

| Carpeta | Contenido |
|---|---|
| `Metronomo/` | App de escritorio para Windows (WinForms + WASAPI vía NAudio). |
| `MetronomoMovil/` | App móvil para Android e iOS (.NET MAUI). |
| `MetronomoMovil/Core/MetronomeEngine.cs` | Motor de audio compartido. |
| `MetronomoMovil/Platforms/Android/AudioPlayer.Android.cs` | Salida de audio Android (`AudioTrack`, baja latencia). |
| `MetronomoMovil/Platforms/iOS/AudioPlayer.iOS.cs` | Salida de audio iOS (`AVAudioEngine`; suena con el modo silencio activado). |

## Descargas

Cada cambio en `main` se compila automáticamente en GitHub Actions
(pestaña **Actions** → última ejecución de **Compilar** → *Artifacts*):

- **Metronomo-Windows**: `Metronomo.exe`, no necesita instalar nada.
- **Metronomo-Android**: `Metronomo-Android.apk`. Cópialo al móvil y permite instalar apps de origen desconocido.
- **Metronomo-iOS-sin-firmar**: `.ipa` sin firmar. Se instala desde Windows o Mac con
  [Sideloadly](https://sideloadly.io/) o [AltStore](https://altstore.io/) usando tu Apple ID.
  Con un Apple ID gratuito la app caduca a los 7 días y hay que reinstalarla.

## Compilar en local

Requisitos: SDK de .NET 10.

```bash
# Escritorio (Windows)
dotnet run --project Metronomo -c Release

# Android (requiere el workload maui-android)
dotnet build MetronomoMovil -c Release -f net10.0-android

# iOS (requiere un Mac con Xcode y el workload maui-ios)
dotnet build MetronomoMovil -c Release -f net10.0-ios
```

> En Windows, si la ruta del proyecto es muy larga, la compilación de Android puede fallar
> (limitación de `aapt2`). Clona el repositorio en una ruta corta, por ejemplo `C:\src\metronomo`.
