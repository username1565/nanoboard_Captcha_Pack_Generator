using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using Chaos.NaCl;
using System.Security.Cryptography;
using System.Linq;

namespace CaptchaPack_Generator
{
	//ByteStringExt.cs - need to show bytearrays as hex, stringify
    public static class ByteStringExt
    {
        public static string Stringify(this byte[] bytes) //show bytes as hex-string
        {
            var sb = new StringBuilder();
            foreach (var b in bytes)
            {
                sb.Append(b.ToString("x2"));
            }
            return sb.ToString();
        }
		
		//int.Parse and Int32.Parse working bad for me.		See issue: https://github.com/nanoboard/nanoboard/issues/5
		//So this function was been writed, to make this code more independent...
        public static int parse_number(this string string_number)//this function return (int)number from (string)"number". Negative numbers supporting too.
        {	if(string_number=="" || string_number == null){Console.WriteLine("NbPack.cs. parse_number. string_number is empty or null: (string_number == \"\"): "+string_number+", (string_number == null): "+(string_number == null)); return 0;}
			string test = (new System.Text.RegularExpressions.Regex(@"\D")).Replace(string_number, "");
            int test_length = test.Length;
            int number = 0;
            for(int i = ((char)test[0]=='-')?1:0; i < test_length; i++){
                number += ((int)Char.GetNumericValue((char)test[i])*(int)Math.Pow(10,test_length-i-1));
			}
            number = ((char)test[0]=='-'?(0-number):(number));
            return number;
        }
	}

	//ByteEncryptionUtil.cs - need to encrypt generated Ed25519-seed, and decrypt it by publicKey and captcha_answer.
    public static class ByteEncryptionUtil
    {
        public static byte[] WrappedXor(byte[] input, string key)
        {
			//one sha hash generated in this case. There can be the chains of different hashes, to make trying the brute-force captcha-value so difficult.
            byte[] sha = SHA512.Create().ComputeHash(Encoding.UTF8.GetBytes(key));
            byte[] output = new byte[input.Length];
            for (int i = 0; i < input.Length; i++)
            {
                output[i] = (byte) (input[i] ^ sha[i & 63]);
            }
            return output;
        }
    }
	
//FileUtil.cs	- to read-write bytes in the specified offset of file or/and append the bytes and text there.
    public static class FileUtil
    {
        public static object _lock = new object();

        /* Appends bytes to the end of file */
        public static int Append(string path, string @string)
        {
            lock (_lock) //sometimes .db3 file busy by another process, when program try to append string there. lock it.
            {
				return Append(path, System.Text.Encoding.UTF8.GetBytes(@string));
			}
        }
	
        /* Appends bytes to the end of file */
        public static int Append(string path, byte[] bytes)
        {
            lock (_lock)
            {
                long pos = 0;
                using (var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.None))
                {
                    pos = stream.Position;
                    stream.Write(bytes, 0, bytes.Length);
					stream.Close();
					stream.Dispose();
                }
                return (int)pos;
            }
        }

        /* Writes bytes at specific file offset, overwrites existing bytes */
        public static void Write(string path, byte[] bytes, int offset)
        {
            lock (_lock)
            {
                using (Stream stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None))
                {
                    stream.Seek(offset, SeekOrigin.Begin);
                    stream.Write(bytes, 0, bytes.Length);
					stream.Close();
					stream.Dispose();
                }
            }
        }
		
        /* Reads bytes from file using specific offset and length */
        public static byte[] Read(string path, int offset, int length)
        {
            var bytes = new byte[length];
            using (Stream stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.Read, FileShare.Read))
            {
                stream.Seek(offset, SeekOrigin.Begin);
                stream.Read(bytes, 0, length);
				stream.Close();
				stream.Dispose();
            }
            return bytes;
        }		
    }//end class FileUtil

	//Generate random captcha text, and return captcha image for this text, with b/w image in Captcha.imageBits (static) or created_captcha_object._imagebits (non-static)
    public class Captcha
    {
//        private static Random Randomizer = new Random(DateTime.Now.Second);	//old code
		private static Random Randomizer = null;		//make this more crypto-strength, and create this object once in Program class.
        public string Text { get; set; }				//Text of generated captcha, empty by default.
        public byte[] ImageAsByteArray { get; set; }	//not compressed image, as bytes, with many collors, empty by default.
        public static byte[] imageBits { get; set; }	//The static field with compressed b/w captcha image. This is available as "Captcha.imageBits", after creating captcha object. Empty by default.
		public byte[] _imageBits { get; set; }			//do the previous field non-static, and now this available as "captcha_object._imageBits" for already created object.
		public static string DataUriPngPrefix = "data:image/png;base64,";
		public string _imageBits_dataURI { get; set; }

        public Captcha(Random set_defined_Randomizer) // Captcha-object constructor
        {
			Randomizer = set_defined_Randomizer;		//set already predefined randomizer
            Text = GetRandomText();						//Generate random captcha-text.
            ImageAsByteArray = CreateCaptcha(Text);		//create static "imageBits" too.
			_imageBits = imageBits;						//copy compressed image from static "imageBits" to non-static field "_imageBits", after creating "imageBits".
			//set _imageBits_dataURI to be able to open this in browser-tab
			using (var ms = new MemoryStream())
			{
				using (var bitmap = Convert(_imageBits))
				{
					bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
					var uri = DataUriPngPrefix + System.Convert.ToBase64String(ms.GetBuffer());
					_imageBits_dataURI = uri;
				}
			}
		}

		//Method to generate random captcha text
        private static string GetRandomText()
        {
            string text = "";													//define empty string
            const string chars = "?abcefgijknopqrsvxyz3478";					//use string with chars, allowed in captcha. chars.Length = 24
            for (int i = 0; i < 5; i++){										//number of captcha symbols = 5
				text += chars.Substring(Randomizer.Next(0, chars.Length), 1);		//generate this 5 symbols
			}
            return text;														//return captcha-text
        }

		//Convert many-colors-bitmap to compressed b/w image as bytearray where pixels are encoded as bits:
        public static byte[] Convert(Bitmap bmp)
        {
            var bytes = new byte[bmp.Width * bmp.Height / 8];
            int bii = 0;
            int byi = 0;
            for (int x = 0; x < bmp.Width; x++)
            {
                for (int y = 0; y < bmp.Height; y++)
                {
                    var pix = bmp.GetPixel(x, y);
                    if (pix.R < 128)	//it so fast!
                    {
                        bytes[byi] |= (byte)(1 << bii);
                    }
                    bii += 1;
                    if (bii >= 8)
                    {   
                        bii = 0;
                        byi += 1;
                    }
                }
            }
            return bytes;
        }
		
		//Convert the bytearray with bits-pixels of compressed b/w image, convert it back -> to b/w bitmap
        public static Bitmap Convert(byte[] bytes, int width = 50, int height = 20)
        {
            var bmp = new Bitmap(width, height);
            int bii = 0;
            int byi = 0;
            for (int x = 0; x < bmp.Width; x++)
            {
                for (int y = 0; y < bmp.Height; y++)
                {
                    var color = Color.White;
                    if ((bytes[byi] & (byte)(1 << bii)) != 0)
                    {
                        color = Color.Black;
                    }
                    bii += 1;
                    if (bii >= 8)
                    {   
                        bii = 0;
                        byi += 1;
                    }
                    bmp.SetPixel(x, y, color);
                }
            }
            return bmp;
        }

		//return captcha image from specified/generated text, and set not compressed image, and compressed image-bits as bytearray Captcha.imageBits
        private static byte[] CreateCaptcha(string text)
        {
            byte[] byteArray = null;
            Font[] fonts = { 
				//old code
//				new Font("Arial", 24, FontStyle.Bold), 
//				new Font("Courier New", 22, FontStyle.Bold), 
//				new Font("Calibri", 20, FontStyle.Bold),
//				new Font("Tahoma", 24, FontStyle.Italic | FontStyle.Bold)

				//fix font-size for image 50x20
				new Font("Arial", 8+Randomizer.Next(6), FontStyle.Bold), 
				new Font("Courier New", 8+Randomizer.Next(6), FontStyle.Bold), 
				new Font("Calibri", 8+Randomizer.Next(6), FontStyle.Bold),
				new Font("Tahoma", 8+Randomizer.Next(6), FontStyle.Italic | FontStyle.Bold)
				//and here, can be added many-many another fonts
			};
			int rotate = 0;	//degress of rotation, after generate each symbol
			bool rotated = (Randomizer.Next(2) == 1);	//random true/false
            using (var bmp = new Bitmap(50, 20))	//for image 50x20 (1000 pixels)
            {
                using (var graphic = Graphics.FromImage(bmp))	//write captcha-image
                {
					using (var hb = new HatchBrush(HatchStyle.DarkUpwardDiagonal, Color.White, Color.White)) graphic.FillRectangle(hb, 0, 0, bmp.Width, bmp.Height); //background is white
					for (int i = 0; i < text.Length; i++)												//for each symbol in captcha-text
                    {
                        var point = new PointF((i * 9)+Randomizer.Next(1)-Randomizer.Next(2), 10);		//select point to draw symbol
						rotate = Randomizer.Next(4);													//generate random degress of rotation
                        graphic.RotateTransform((rotated == true) ? rotate : 0-rotate);					//rotate or rotate-back
						rotated = !rotated;																//change rotation bool true/false/true/false
                        graphic.DrawString(																//and draw the current symbol
							text.Substring(i, 1),															//take this symbol
							fonts[Randomizer.Next(0, fonts.Length)],										//select random font
							Brushes.Black,																	//write this as black
							point,																			//in selected point
							new StringFormat { LineAlignment = StringAlignment.Center }						//write this symbol as string
						);
                    }
                }
                using (var stream = new MemoryStream()) {	//after generate many-colors image with captcha
					imageBits = Convert(bmp);					//make the compressed image from that bmp, and save it in static field Captcha.ImageBits, as bytearray
                    bmp.Save(stream, ImageFormat.Png);			//save old bmp in MemoryStream
                    byteArray = stream.ToArray();				//and return this as bytearray
                }
            }
            foreach (var font in fonts) font.Dispose();		// Cleanup Fonts (they are disposable)
            return byteArray;	//and return not compressed image, as bytearray
        }

    }//end class Captcha
	
	public class Program{
		//make this Randomizer more crypto-strength, and define this once
		private static Random Randomizer = 	new Random(
														(
															(int)
															(
																(long)
																(
																	(
																		(long)
																		(DateTime.Now - new DateTime(1970, 1, 1))
																		.TotalMilliseconds
																	)
																	+
																	(
																		(long)
																		(DateTime.Now.Ticks)
																	)
																)
																%
																(long)
																(int.MaxValue)
															)
														)
											)
		;	//end define the Randomizer
	//Define the another public variables, about captcha:
			//189 bytes in block in offsets (captcha_index * 189); 189 = 32 + 32 + 125 (1000 bits for pixels of b/w image 20x50)
			public static string 	@captchaPackFilename 			= "captcha_pack.nbc"	;				//captcha-pack-file
			public static int 		CaptchaBlockLength 				= 189					;				//bytelength of one captcha-block: 32 bytes + 32 bytes + 125 bytes (1000 bits);
		
			//there is possible to logging anwsers, because solved once captcha can be used again, by index.
			public static string 	@captchas_answers_file 			= "answers.bin"			;				//5 bytes with symbols in block in offsets (captcha_index * 5)
			public static int 		CaptchaAnswerLength 			= 5						;				//bytelength of one captcha-answer: 5 symbols = 5 bytes;
			//show dot in console, after each "one_dot" iterations.
			public static int		one_dot							= 1024;														//	show one dot, when this number of captchas was been generated.
			public static int		block_length_to_write			= 16384;													//	block length to write
			public static byte[]	captchas_block_to_write			= new byte[block_length_to_write*CaptchaBlockLength]	;	//	write captchas by blocks 16384 captchas, to minimize the actions of writes.
			public static byte[]	captcha_answers_block_to_write	= new byte[block_length_to_write*CaptchaAnswerLength]	;	//	write captcha-anwers by blocks 16384 answers, to minimize the actions of writes.
			public static bool		save_the_captcha_answers 		= 	false;		//if need to write the generated anwsers (optionally value, and disabled, false, by default).
			
			
	//method to generate captcha-pack-file
		public static void generate_captcha_pack_file(bool save_captcha_images_as_files = false, bool verify_captcha_answer_before_writting = true, bool logging = false, int set_number_of_captchas_for_one_dot = 1024, int set_block_length_to_write = 16384){
			one_dot 				= set_number_of_captchas_for_one_dot;
			block_length_to_write 	= set_block_length_to_write;
			
            byte[]	ed25519_seed = new byte[32];								//define the ed25519_seed array with length 32 bytes to store randomly generated Ed25519-seed.
            System.Security.Cryptography.RNGCryptoServiceProvider rand = new System.Security.Cryptography.RNGCryptoServiceProvider();		//initialize object once, to take crypto-strength random there on each iteration.
			Console.WriteLine("Starting to generate captchas...");
			Console.WriteLine("save_captcha_images_as_files = "+save_captcha_images_as_files+", verify_captcha_answer_before_writting = "+verify_captcha_answer_before_writting+", logging = "+logging+", set_number_of_captchas_for_one_dot = "+set_number_of_captchas_for_one_dot+", set_block_length_to_write = "+set_block_length_to_write);			
			Console.WriteLine("Each dot means already was been generated "+one_dot+" captchas,\nand this is contains in bytearray-buffer with size: "+block_length_to_write);
			//Start to generate 1024*1024 different captchas:
			for(int i = 0; i<1048576; i++){
				//create new captcha with public static Randomizer, which was been already defined once. Don't create this Randomizer again and again for each object.	
				Captcha captcha_image = new Captcha(Randomizer);
				//	generate in the cycle the many bitmaps 1000 bits = 1000 pixels, black or white, 20x50 with captchas, and save this with answer filename.

				if	(	save_captcha_images_as_files 	== 	true	){	//as optional param.
					save_captchas_as_images(i, captcha_image); 				//	Save image in file

					//					test stop
					if(i>10){break;}	//generate no more than 10 captcha images, and stop.
				}//Good working, and ok.

//				Now, don't save captcha images as PNG-files, and pack it in captcha-pack.nbc
//					Each block of captcha-pack-file contains ed25519_public_key(32 bytes) + encrypted_seed(32 bytes) + 1000 bits captcha image(125 bytes * 8 = 1000 bits).

				//	To to this, need to gererate random seed,
				//	thien get Ed25519 pubkey from this (privkey is seed + pub)
				//	encrypt this pubkey by WrappedXOR with captcha_answer,
				//	and save, then:
				//		public key 					(32 bytes)
				//		encrypted seed 				(32 bytes)
				//		captcha image 1000 bits		(189 bytes)
				//	Save this all in the each block of captcha-pack file.

				rand.GetBytes(ed25519_seed);																//generate ed25519_seed randomly
				byte[] ed25519_seed_and_publicKey = Ed25519.ExpandedPrivateKeyFromSeed(ed25519_seed);		//private key = ed25519_seed + public key
				byte[] publicKey = new byte[32];													//define 32-bytes bytearray, to extract public key, as 32 last bytes.
				System.Buffer.BlockCopy(	ed25519_seed_and_publicKey, 				32, publicKey, 	0, 			32		);		//do extract the public key

				//Encrypt seed by captcha answer
				//	seed is the generated random in "ed25519_seed", captcha_image.Text = answer,
				//	Public Key - public-key (32 bytes, will be writted in block of captcha-pack-file),
				//	not private key (seed + pub)
				byte[] encrypted_ed25519_seed = ByteEncryptionUtil.WrappedXor(ed25519_seed, 			(captcha_image.Text) 			+ publicKey.Stringify());	//encrypt ed25519_seed as previous seed.

				//build the block with one captcha (32 bytes + 32 bytes + 1000 bits)
				byte[] captcha_block = new byte[	32	+	32	+	125	];

				System.Buffer.BlockCopy(	publicKey, 						0, captcha_block, 	0, 			32		);	//write there the pub
				System.Buffer.BlockCopy(	encrypted_ed25519_seed, 				0, captcha_block, 	32, 		32		);	//encrypted seed
//				System.Buffer.BlockCopy(	Captcha.imageBits, 				0, captcha_block, 	32 + 32, 	125		);	//and 1000 bits of captcha image.
				System.Buffer.BlockCopy(	captcha_image._imageBits, 				0, captcha_block, 	32 + 32, 	125		);	//and 1000 bits of captcha image.

				System.Buffer.BlockCopy(
					captcha_block,											//the current captcha block
					0,														//from 0 offset
					captchas_block_to_write,								//write here
					(i%block_length_to_write) * CaptchaBlockLength,			//in this offset
					CaptchaBlockLength										//all the current byte-block
				);
																			//and...
				System.Buffer.BlockCopy(
					System.Text.Encoding.UTF8.GetBytes(captcha_image.Text),	//the captcha answer, as bytes
					0,														//from 0 offset
					captcha_answers_block_to_write,							//write here
					(i%block_length_to_write) * CaptchaAnswerLength,		//in this offset
					CaptchaAnswerLength										//all the current byte-block
				);

				byte[] decrypted_ed25519_seed 				=	new byte[32];						//define bytearray to save decrypted_ed25519_seed
				//define two test bytearrays with length 32 bytes
				byte[] test_ed25519_seed_and_publicKey		= 	new byte[64];						//define bytearray to save privkey (seed and pubkey)	- 64 bytes
				byte[] test_new_pubkey 						= 	new byte[32];						//define bytearray extract pubkey						- 32 bytes
				bool verified = false;			//define this variable as false. This will be true, if captcha-answer is valid.

				if(		verify_captcha_answer_before_writting	==	true	){	//as optional param
					//test code to check all values, before writting this in captcha-pack-file:
					//decrypt this, just for test.
					decrypted_ed25519_seed = ByteEncryptionUtil.WrappedXor(encrypted_ed25519_seed, 	(captcha_image.Text) 			+ publicKey.Stringify());
					//compare decrypted seed with original ed25519_seed:
				//	if(CompareTwoArrays(decrypted_ed25519_seed, ed25519_seed)){
				//		Console.WriteLine("decrypted_ed25519_seed and ed25519_seed arrays are equals!");
				//	}
						
					//	extract privkey and pubkey from decrypted seed
					test_ed25519_seed_and_publicKey = Ed25519.ExpandedPrivateKeyFromSeed(decrypted_ed25519_seed);
					System.Buffer.BlockCopy(	test_ed25519_seed_and_publicKey, 	32, test_new_pubkey, 	0, 	32	);		//do extract the public key
					//	or do this by one command:
					//Ed25519.KeyPairFromSeed(out test_new_pubkey, out test_ed25519_seed_and_publicKey, ByteEncryptionUtil.WrappedXor(encrypted_ed25519_seed, (captcha_image.Text) + publicKey.Stringify()));
					
					//compare pubkey with previous extracted pubkey.
					if(CompareTwoArrays(test_new_pubkey, publicKey)){
						verified = true;
					}

					//update verified value by checking the captcha-answer using method to do this.
					verified = check_captcha_answer_for_index(i, (captcha_image.Text));		//write data or/and show progress will be if captcha is solved

					if(verified == false){
						//fill both previous blocks with null-bytes
						System.Buffer.BlockCopy(
							new byte[CaptchaBlockLength],							//null bytes block with the current captcha block length
							0,														//from 0 offset
							captchas_block_to_write,								//write here
							(i%block_length_to_write) * CaptchaBlockLength,			//in this offset
							CaptchaBlockLength										//all the current byte-block
						);
																				//and
						System.Buffer.BlockCopy(
							new byte[CaptchaAnswerLength],							//null bytes block with the current captcha answer length
							0,														//from 0 offset
							captcha_answers_block_to_write,							//write here
							(i%block_length_to_write) * CaptchaAnswerLength,		//in this offset
							CaptchaAnswerLength										//all the current byte-block
						);
						i -= 1;														//back to previous captcha index
						continue;													//and continue the cycle of generation the captchas
					}
				}
				else{
					show_progress_and_write_data(i);								//just write block in data or/and show progress without verification of captcha_answer
				}

				if(logging == true){		//as optional param.
				//Just for test - do logging the values of all variables:
					FileUtil.Append(
						@"generation.log",
						"index: i = "+i
						+"\n(captcha_image.Text): "					+	(captcha_image.Text)
						+"\ned25519_seed(seed): "					+	ed25519_seed.Stringify()
						+"\nseed_and_publicKey: "					+	ed25519_seed_and_publicKey.Stringify()
						+"\npublicKey: "							+	publicKey.Stringify()
						+"\nencrypted_ed25519_seed: "				+	encrypted_ed25519_seed.Stringify()
						+(
							(verify_captcha_answer_before_writting)
								?	"\ndecrypted_ed25519_seed: "											+	decrypted_ed25519_seed.Stringify()
									+"\nCompareTwoArrays(decrypted_ed25519_seed, ed25519_seed): "			+	CompareTwoArrays(decrypted_ed25519_seed, ed25519_seed)
									+"\ntest_new_pubkey: "													+	test_new_pubkey.Stringify()
									+"\ntest_ed25519_seed_and_publicKey: "									+	test_ed25519_seed_and_publicKey.Stringify()
									+"\nCompareTwoArrays(test_new_pubkey, publicKey): "						+	CompareTwoArrays(test_new_pubkey, publicKey)
									+"\nverified: "															+	verified
								:	""
						)
						+"\ncaptcha_block: "						+	captcha_block.Stringify()
						+"\ncaptcha_image.ImageAsByteArray: "		+	captcha_image.ImageAsByteArray.Stringify()	//not compressed image, as bytearray
						+"\nCaptcha.imageBits: "					+	Captcha.imageBits.Stringify()				//get compressed b/w bit-image from static field of current created captcha-object
						+"\ncaptcha_image._imageBits: "				+	captcha_image._imageBits.Stringify()		//get compressed b/w bit-image from specified captcha-object
						+"\ncaptcha_image._imageBits_dataURI:\n"	+	captcha_image._imageBits_dataURI	//this can be opened in browser tab to see captcha.
						+"\n\n\n\n\n\n"
					);
				}		//end if logging
			}		//end brute-force cycle
		}		//end generate_captcha_pack_file method
		
		public static bool check_captcha_answer_for_index(int index, string captcha_answer){
			byte[] publicKey 		= 	new byte[32];
			byte[] encryptedSeed	=	new byte[32];
//			byte[] image 			=	new byte[125];
			System.Buffer.BlockCopy(captchas_block_to_write, 	(index%block_length_to_write * CaptchaBlockLength), 			publicKey,		0,		32	);	//get public key		from block
            System.Buffer.BlockCopy(captchas_block_to_write, 	(index%block_length_to_write * CaptchaBlockLength) + 32, 		encryptedSeed, 	0,		32	);	//get encrypted_seed	from block
//			System.Buffer.BlockCopy(captchas_block_to_write, 	(index%block_length_to_write * CaptchaBlockLength) + 32 + 32,	image,			0,		125	);	//get bits of compressed captcha-image
			byte[]	new_pubkey 	= 	new byte[32];
			byte[]	new_privkey = 	new byte[32];
			Ed25519.KeyPairFromSeed(out new_pubkey, out new_privkey, ByteEncryptionUtil.WrappedXor(encryptedSeed, captcha_answer + publicKey.Stringify()));
			if(CompareTwoArrays(new_pubkey, publicKey)){
				show_progress_and_write_data(index);								//write blocks in file or show progress.
				//Console.WriteLine("Captcha index = "+index+" is solved!");
				return true;
			}else{
				return false;
			}
		}
		
		//	Save image into file, and put the answer in filename. Replace '?' to '_', because '?' not allowed in filename.			
		public static void save_captchas_as_images(int i, Captcha captcha_image, bool bw = false){
				using(
					Bitmap bmp = (
									(bw == true)
										?//save captcha image as compressed black-white-only PNG, which was been encoded by 1000 bits only.
											Captcha.Convert(captcha_image._imageBits)
										:// or take image of this captcha, as not compressed image-bytearray, and save it as PNG (many colors there)
											(Bitmap)Bitmap.FromStream(new MemoryStream(captcha_image.ImageAsByteArray))
					)
				)
				{
					string current_captcha_filename = (	i.ToString("D6")	+	"="	+	(captcha_image.Text).Replace('?', '_')	+	".png"	);
					bmp.Save(@current_captcha_filename, ImageFormat.Png);  // Or Png
				}
		}

		public static void show_progress_and_write_data(int index){
			if(index%one_dot == 0 && index!=0){
				Console.Write(".");
			}
			if(index%block_length_to_write == 0 && index!=0){
				Console.Write("\n"+index+" captchas generated, and writted in the files.\n");
				FileUtil.Append(captchaPackFilename, captchas_block_to_write);				//append block in the end of captcha_pack-file
				if(save_the_captcha_answers == true){	FileUtil.Append(captchas_answers_file, captcha_answers_block_to_write);	}	//just append the answer to solve this capthca in text-file.
				captchas_block_to_write				= new byte[block_length_to_write*CaptchaBlockLength];
				captcha_answers_block_to_write		= new byte[block_length_to_write*CaptchaAnswerLength];
			}
		}
		
		//Just simply method to compare bytearrays and to test it
		public static bool CompareTwoArrays(byte[] array1, byte[] array2)
		{
			return !array1.Where((t, i) => t != array2[i]).Any();
		}
		
		//	start Program to generate captchas...
		public static void Main(string[] args){
			bool save_captcha_images_as_files 				=	false;
			bool verify_captcha_answer_before_writting 		=	true;
			bool logging 									=	false;
			int set_one_dot 								=	1024;
			int set_block_size 								=	16384;
			
			if(args.Length>=1){		if (bool.TryParse(args[0]		, out save_captcha_images_as_files))			{}		}
			if(args.Length>=2){		if (bool.TryParse(args[1]		, out verify_captcha_answer_before_writting))	{}		}
			if(args.Length>=3){		if (bool.TryParse(args[2]		, out logging))									{}		}
			if(args.Length>=4){		if (int.TryParse(args[3]		, out set_one_dot))								{set_one_dot 		= (args[3]).parse_number();}		} //sometimes int.Parse, and Int32.Parse working incorrect
			if(args.Length>=5){		if (Int32.TryParse(args[4]		, out set_block_size))							{set_block_size 	= (args[4]).parse_number();}		} //so with parse_number this working good.
			
			generate_captcha_pack_file(save_captcha_images_as_files, verify_captcha_answer_before_writting, logging, set_one_dot, set_block_size);
		}//end Main

	}//end class Program
}//end CaptchaPack_Generator namespace