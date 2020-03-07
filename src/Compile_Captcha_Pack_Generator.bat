set fdir=%WINDIR%\Microsoft.NET\Framework
set csc=%fdir%\v4.0.30319\csc.exe
::		Chaos.NaCl.dll - dll in "../".
::		Compiled Captcha_Pack_Generator.exe working with this dll, and not working without this.
::		The source code of this dll (Dynamic Link Library) - here: https://github.com/CodesInChaos/Chaos.NaCl
::		This need to "using Chaos.NaCl", and working with Ed25519, then (generate keypair, get public key from seed, etc...)
%csc% /t:exe /reference:"../Chaos.NaCl.dll" /out:"../Captcha_Pack_Generator.exe" Captcha_Pack_Generator.cs
cd ..
Captcha_Pack_Generator.exe
pause