using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using IO = System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using TagLib;
using TagLib.Id3v2;
using File = TagLib.File;

namespace TagDumper
{
    class Program
    {
        static void Main(string[] args)
        {
            // Verify that there is a file path provided
            if (args.Length != 1)
            {
                Console.Error.WriteLine("*** File not provided");
                PrintUsage();
                return;
            }

            // Verify that the file path provided exists
            string filePath = args[0];
            if (!IO.File.Exists(filePath))
            {
                Console.Error.WriteLine("*** File {0} does not exist", filePath);
                PrintUsage();
            }

            using (IO.FileStream fs = IO.File.OpenRead(filePath))
            {
                DumpTrackMetadata(fs, null);
            }

            Console.WriteLine("Press Enter to Exit...");
            Console.ReadLine();
        }

        static void PrintUsage()
        {
            Console.WriteLine("Usage: TagDumper.exe file");
            Console.WriteLine("    file: path to file to dump tags for");
        }

        private static void WriteLineWithIndent(int indent, string s)
        {
            // Figure out how many lines we need
            int widthAfterIndent = Console.WindowWidth - indent;
            for (int i = 0; i < s.Length; i += widthAfterIndent)
            {
                for (int j = 0; j < indent; j++)
                {
                    Console.Write(" ");
                }
                int charsToWrite = Math.Min(s.Length - i, widthAfterIndent);
                Console.Write(s.Substring(i, charsToWrite));
            }
            Console.WriteLine();
        }

        /// <summary>
        /// The list of image codecs that the System.Drawing.Imaging library supports
        /// </summary>
        private static readonly ImageCodecInfo[] ImageCodecs = ImageCodecInfo.GetImageEncoders();

        public static void DumpTrackMetadata(IO.FileStream file, string mimetype)
        {
            // Load up the file using TagLib
            File tagFile;
            try
            {
                tagFile = File.Create(new StreamFileAbstraction(file.Name, file, null), mimetype, ReadStyle.Average);
            }
            catch (UnsupportedFormatException)
            {
                Console.Error.WriteLine("*** The file format is not supported.");
                return;
            }

            if (tagFile.PossiblyCorrupt)
            {
                Console.Error.WriteLine("*** The file is possibly corrupt. Reasons:");
                Console.Error.WriteLine("    {0}", String.Join(",", tagFile.CorruptionReasons));
            }

            // Fetch the metadata from the file
            if (tagFile.TagTypesOnDisk.HasFlag(TagTypes.Xiph))
            {
                Console.WriteLine("-----------------");
                Console.WriteLine("Found XIPH Tags");
                DumpXiphMetadata(tagFile);
            }
            if (tagFile.TagTypesOnDisk.HasFlag(TagTypes.Id3v2))
            {
                Console.WriteLine("-----------------");
                Console.WriteLine("Found ID3v2");
                DumpId3V2Metadata(tagFile);
            }
            if (tagFile.TagTypesOnDisk.HasFlag(TagTypes.Id3v1))
            {
                Console.WriteLine("-----------------");
                Console.WriteLine("Found ID3v1 Tags");
                //ReadId3V1Metadata(result, tagFile);
            }
            else if (tagFile.TagTypes.HasFlag(TagTypes.Asf))
            {
                //ReadAsfMetadata(result, tagFile);
            }
            else
            {
                //throw new DolomiteInternalException(null, "The file format is not supported.",
                //    String.Format("Mimetype {0} was read correctly, no supported TagTypesOnDisk are available. TagTypes: {1}",
                //        mimetype, tagFile.TagTypesOnDisk));
            }

            // Read the codec details and, optionally, the album art
            //DumpCodecDetails(tagFile);
            //ReadPictureDetails(result, tagFile);
        }

        public static void DumpXiphMetadata(File tagFile)
        {
            TagLib.Ogg.XiphComment tags = (TagLib.Ogg.XiphComment)tagFile.GetTag(TagTypes.Xiph);

            foreach (string fieldName in tags.Distinct().OrderBy(t => t))
            {
                string[] fieldValue = tags.GetField(fieldName);
                if (fieldValue.Length > 1)
                {
                    Console.WriteLine("{0} ({1}):", fieldName, fieldValue.Length);
                    foreach (string fv in fieldValue)
                    {
                        WriteLineWithIndent(4, fv);
                    }
                }
                else if (fieldValue.Length == 1)
                {
                    Console.WriteLine("{0}:", fieldName);
                    WriteLineWithIndent(4, fieldValue[0]);
                }
                else
                {
                    Console.WriteLine("{0} (No Value)", fieldName);
                }
            }
        }

        public static void DumpId3V2Metadata(File tagFile)
        {
            TagLib.Id3v2.Tag tags = (TagLib.Id3v2.Tag) tagFile.GetTag(TagTypes.Id3v2);
            foreach (SimpleFrame f in tags.Select(f => new SimpleFrame(f)).OrderBy(f => f.FrameId))
            {
                if (f.Value.Length > 1)
                {
                    Console.WriteLine("{0} ({1}):", f.FrameId, f.Value.Length);
                    foreach (string fv in f.Value)
                    {
                        WriteLineWithIndent(4, fv);
                    }
                }
                else if (f.Value.Length == 1)
                {
                    Console.WriteLine("{0}:", f.FrameId);
                    WriteLineWithIndent(4, f.Value[0]);
                }
                else
                {
                    Console.WriteLine("{0} (No Value)", f.FrameId);
                }
            }   
        }

        private class SimpleFrame
        {
            public string FrameId { get; private set; }
            public string[] Value { get; private set; }

            public SimpleFrame(Frame frame)
            {
                FrameId = Encoding.ASCII.GetString(frame.FrameId.ToArray());
                Dictionary<Type, Action<Frame>> typeActions = new Dictionary<Type, Action<Frame>>
                {
                    {typeof(TextInformationFrame), f => { Value = ((TextInformationFrame) f).Text; }},
                    {typeof(CommentsFrame), f => { Value = new[] {((CommentsFrame) f).Text}; }},
                    {
                        typeof(PrivateFrame), f =>
                        {
                            var data = ((PrivateFrame) f).PrivateData.ToArray();
                            Value = new[] {Encoding.ASCII.GetString(data)};
                        }
                    },
                    {typeof(AttachedPictureFrame), f => { Value = new [] {"[Attached Picture Frame]"}; } },
                    {typeof(UserTextInformationFrame), f =>
                    {
                        var data = (UserTextInformationFrame) f;
                        FrameId += String.Format(" ({0})", data.Description);
                        Value = data.Text;
                    } }
                };

                if (typeActions.ContainsKey(frame.GetType()))
                {
                    typeActions[frame.GetType()](frame);
                }
                else
                {
                    Value = new[] {"-UNKNOWN FRAME TYPE-"};
                }
            } 
        }


        /// <summary>
        /// Extracts codec information. At most basic, the codec used is determined. For types with
        /// subtypes (such as MP3's VBR/CBR) this information is processed into the codec string.
        /// </summary>
        /// <param name="md">The metadata object to store the image details to</param>
        /// <param name="tagFile">The TagLib abstraction to extract tags from</param>
        //private static void ReadCodecDetails(File tagFile)
        //{
        //    Console.WriteLine("Duration: {0}", TimeSpan.FromMilliseconds(tagFile.Properties.Duration.TotalMilliseconds));

        //    ICodec codec = tagFile.Properties.Codecs.First();
        //    if (codec is TagLib.Mpeg.AudioHeader)
        //    {
        //        TagLib.Mpeg.AudioHeader mp3Codec = (TagLib.Mpeg.AudioHeader)codec;
        //        md.BitrateKbps = mp3Codec.AudioBitrate;
        //        md.Codec = "MP3 / ";
        //        if (mp3Codec.VBRIHeader.Present || mp3Codec.XingHeader.Present)
        //        {
        //            md.Codec += "VBR";
        //        }
        //        else
        //        {
        //            md.Codec += "CBR";
        //        }
        //        md.Extension = "mp3";
        //    }
        //    else if (codec is TagLib.Flac.StreamHeader)
        //    {
        //        TagLib.Flac.StreamHeader flacCodec = (TagLib.Flac.StreamHeader)codec;
        //        md.BitrateKbps = flacCodec.AudioBitrate;
        //        md.Codec = "FLAC";
        //        md.Extension = "flac";
        //    }
        //    else if (codec is TagLib.Ogg.Codecs.Vorbis)
        //    {
        //        TagLib.Ogg.Codecs.Vorbis vorbisCodec = (TagLib.Ogg.Codecs.Vorbis)codec;
        //        md.BitrateKbps = vorbisCodec.AudioBitrate;
        //        md.Codec = "Vorbis";
        //        md.Extension = "ogg";
        //    }
        //}

        /// <summary>
        /// Uses the Tag abstraction from TagLib to determine whether or not a track has an
        /// attached image and if so, what its dimensions are. Dimension calculation is performed
        /// using the System.Drawing libraries.
        /// </summary>
        /// <param name="md">The metadata object to store the image details to</param>
        /// <param name="tagFile">The TagLib abstraction to extract tags from</param>
        //private static void ReadPictureDetails(TrackMetadata md, File tagFile)
        //{
        //    // Do stuff to figure out if it has a picture attached
        //    if (tagFile.Tag.Pictures.Length > 0)
        //    {
        //        md.ImageBytes = tagFile.Tag.Pictures.First().Data.Data;
        //    }
        //    else if (tagFile.TagTypesOnDisk.HasFlag(TagTypes.Xiph))
        //    {
        //        TagLib.Ogg.XiphComment xiph = (TagLib.Ogg.XiphComment)tagFile.GetTag(TagTypes.Xiph);
        //        string imageField = xiph.GetFirstField("METADATA_BLOCK_PICTURE");
        //        if (!String.IsNullOrWhiteSpace(imageField))
        //        {
        //            FlacImage flacImage = new FlacImage(imageField);
        //            md.ImageBytes = flacImage.ImageBytes;
        //        }
        //    }

        //    // Figure out the mimetype so we can store it
        //    if (md.ImageBytes != null)
        //    {
        //        using (MemoryStream ms = new MemoryStream(md.ImageBytes))
        //        {
        //            Image imageObj = Image.FromStream(ms);
        //            md.ImageMimetype = ImageCodecs.First(c => c.FormatID == imageObj.RawFormat.Guid).MimeType;
        //        }
        //    }
        //}
    }
}
