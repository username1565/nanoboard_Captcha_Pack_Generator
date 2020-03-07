# nanoboard_Captcha_Pack_Generator
Captcha generator for nanoboard

This generator contains 500 lines of C# code,

and allow to generate captcha-pack-file for [nanoboard](https://github.com/username1565/nanoboard).

See Releases, to download compiled ".exe", or compile this yourself, by running "src/Compile_Captcha_Pack_Generator.bat".



This program do generate the captchas, and write this in byte-array-buffer,

and then, write the captcha_pack.nbc by blocks with 16384 captchas.

Each dot in the program, means 1024 captchas was been already generated,

and after buffer contains 16384 captchas, this will be saved in the file.

Optionally is possible to write the generated captcha answers, but this is disabled by default.

No need to save any captcha answers, to prevent the leaks of this values, and the wipe nanoboard, and spam then.



As you can see, in the source code, and after test this simply program,

the file captcha_pack.nbc is easy to generate,

but so hard and so difficult to solve all captchas there.
