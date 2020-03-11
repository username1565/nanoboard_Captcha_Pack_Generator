set fdir=%WINDIR%\Microsoft.NET\Framework
set csc=%fdir%\v4.0.30319\csc.exe
::		Chaos.NaCl.cs - instead the Chaos.NaCl.dll in "../".
::		Compiled Captcha_Pack_Generator.exe was working with that dll, but now this is standalone exe.
::		The source code of this dll (Dynamic Link Library) - here: https://github.com/CodesInChaos/Chaos.NaCl was been concentrated in one cs-file.
::		This need to "using Chaos.NaCl", and working with Ed25519, then (generate keypair, get public key from seed, etc...)
%csc% /t:exe /out:"../Captcha_Pack_Generator.exe" Chaos.NaCl.cs Captcha_Pack_Generator.cs
cd ..
Captcha_Pack_Generator.exe
pause