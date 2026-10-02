# Discord Rich Presence Installer

Instalador de escritorio para Windows que prepara Git, Python y Discord Rich Presence Desktop desde el repositorio público.

## Requisitos para compilar

- Windows 10 u 11.
- .NET Framework 4.8 Developer Pack y MSBuild.
- WinGet (Instalador de aplicaciones).

Ejecuta `build.bat` para compilar `bin\DiscordRichPresenceInstaller.exe`.

## Qué hace

1. Muestra una advertencia y solicita confirmación antes de instalar.
2. Comprueba Git, Python 3.11+ y WebView2 Runtime; instala lo que falte usando WinGet.
3. Clona `UiUyHerrera/discord-rich-presence-desktop` en `%LOCALAPPDATA%\Programs\EstadoDiscord`.
4. Crea un entorno virtual aislado e instala `requirements.txt`.
5. Crea un acceso directo en el menú Inicio y, opcionalmente, en el escritorio.
6. Puede abrir la aplicación al finalizar.

La instalación requiere conexión a Internet, WinGet y acceso a GitHub y PyPI. Los instaladores de Git, Python y WebView2 pueden mostrar solicitudes de permisos de Windows.

## Advertencia de Discord

La aplicación actual utiliza el token de una cuenta de usuario. La automatización de cuentas de usuario puede infringir los términos de Discord y provocar la suspensión de la cuenta. El instalador no solicita ni guarda tokens; la advertencia se muestra antes de empezar y la aplicación pide el token al abrirse.

La configuración y el token de la aplicación se guardan fuera de la carpeta de instalación, en `%APPDATA%\EstadoDiscord`.
