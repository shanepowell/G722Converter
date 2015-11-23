using System;
using System.IO;
using CommandLine;
using CommandLine.Text;
using NAudio.Codecs;
using NAudio.Wave;

namespace G722Converter
{
    public class G722ConverterOptions
    {
        // ReSharper disable UnusedAutoPropertyAccessor.Global
        [Option('g', "g722", Required = false, HelpText = "G722 data to/from be converted.")]
        public string G722File { get; set; }

        [Option('w', "wav", Required = false, HelpText = "wav file to/from be converted.")]
        public string AudioFile { get; set; }

        [Option('b', "bitrate", Required = false, DefaultValue = 64000, HelpText = "G.722 bitrate")]
        public int G722BitRate { get; set; }

        [Option('s', "samplerate", Required = false, DefaultValue = 8000, HelpText = "WAV sameple rate")]
        public int WavSampleRate { get; set; }

        // ReSharper disable once UnusedMember.Global
        [ParserState]
        public IParserState LastParserState { get; set; }

        // ReSharper disable once UnusedMember.Global
        [HelpOption]
        public string GetUsage()
        {
            return HelpText.AutoBuild(this, current => HelpText.DefaultParsingErrorsHandler(this, current));
        }
        // ReSharper restore UnusedAutoPropertyAccessor.Global
    }

    internal static class Program
    {
        private static void Main(string[] args)
        {
            try
            {
                var options = new G722ConverterOptions();
                if (Parser.Default.ParseArguments(args, options))
                {
                    if (File.Exists(options.G722File))
                    {
                        ConvertG722FileToWavFile(options.G722File, GetWaveFilePath(options.G722File, options.AudioFile), options.G722BitRate, options.WavSampleRate);
                    }
                    else if (File.Exists(options.AudioFile))
                    {
                        ConvertAudioFileToG722File(options.AudioFile, GetG722FilePath(options.AudioFile, options.G722File), options.G722BitRate, options.WavSampleRate);
                    }
                    else
                    {
                        Console.WriteLine("No valid input file!");
                        Console.Write(options.GetUsage());
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        private static string GetG722FilePath(string wavFile, string g722File)
        {
            if (!string.IsNullOrEmpty(g722File)) return g722File;

            var g722Filename = Path.GetFileNameWithoutExtension(wavFile) + ".g722";
            var rootPath = Path.GetDirectoryName(wavFile);
            if (!string.IsNullOrEmpty(rootPath))
            {
                g722Filename = Path.Combine(rootPath, g722Filename);
            }

            return g722Filename;
        }

        private static string GetWaveFilePath(string g722File, string wavFile)
        {
            if (!string.IsNullOrEmpty(wavFile)) return wavFile;

            var wavFilename = Path.GetFileNameWithoutExtension(g722File) + ".wav";
            var rootPath = Path.GetDirectoryName(g722File);
            if (!string.IsNullOrEmpty(rootPath))
            {
                wavFilename = Path.Combine(rootPath, wavFilename);
            }

            return wavFilename;
        }

        private static void ConvertG722FileToWavFile(string g722InputFile, string wavOutputFile, int g722BitRate, int wavSampleRate)
        {
            try
            {
                Console.WriteLine("Converting [{0}] to [{1}]", g722InputFile, wavOutputFile);

                Console.WriteLine("Reading [{0}]", g722InputFile);
                var g722Data = File.ReadAllBytes(g722InputFile);

                Console.WriteLine("Decoding G722 Audio [{0}]", g722InputFile);
                var audioData = DecodeG722(g722Data, g722BitRate);

                Console.WriteLine("Writing wav file [{0}]", wavOutputFile);
                WriteWavFile(wavOutputFile, audioData, wavSampleRate);

                Console.WriteLine("G.722 Conversation Complete");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        private static void ConvertAudioFileToG722File(string audioInputFile, string g722OuputFile, int g722BitRate, int wavSampleRate)
        {
            try
            {
                Console.WriteLine("Converting [{0}] to [{1}]", audioInputFile, g722OuputFile);

                Console.WriteLine("Reading WAV Audio [{0}]", audioInputFile);
                var pcmData = ReadWavDataIntoMonoPcm(audioInputFile, wavSampleRate);

                Console.WriteLine("Encode g722 data");
                var g722Data = EncodeG722(pcmData, g722BitRate);

                Console.WriteLine("Writing g722 file [{0}]", g722OuputFile);
                WriteG722File(g722OuputFile, g722Data);

                Console.WriteLine("WAV Conversation Complete");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        private static byte[] ReadWavDataIntoMonoPcm(string audioInputFile, int samepleRate)
        {
            using (var reader = new WaveFileReader(audioInputFile))
            {
                var newFormat = new WaveFormat(samepleRate, 16, 1);
                using (var conversationStream = new WaveFormatConversionStream(newFormat, reader))
                using (var pcmStream = WaveFormatConversionStream.CreatePcmStream(conversationStream))
                {
                    var buffer = new byte[pcmStream.Length];
                    var bytesRead = pcmStream.Read(buffer, 0, buffer.Length);

                    if (bytesRead != buffer.Length)
                    {
                        var outputBuffer = new byte[bytesRead];
                        Buffer.BlockCopy(buffer, 0, outputBuffer, 0, bytesRead);
                        return outputBuffer;
                    }

                    return buffer;
                }
            }
        }

        private static byte[] DecodeG722(byte[] g722Data, int sampleRate)
        {
            var codec = new G722Codec();
            var state = new G722CodecState(sampleRate, G722Flags.SampleRate8000);

            var decodedLength = g722Data.Length * 2;
            var outputBuffer = new byte[decodedLength];
            var wb = new WaveBuffer(outputBuffer);
            var length = codec.Decode(state, wb.ShortBuffer, g722Data, g722Data.Length) * 2;

            if (length != outputBuffer.Length)
            {
                var outputBuffer2 = new byte[length];
                Buffer.BlockCopy(outputBuffer, 0, outputBuffer2, 0, length);
                outputBuffer = outputBuffer2;
            }

            return outputBuffer;
        }

        private static byte[] EncodeG722(byte[] pcmMonoData, int sampleRate)
        {
            var codec = new G722Codec();
            var state = new G722CodecState(sampleRate, G722Flags.SampleRate8000);

            var wb = new WaveBuffer(pcmMonoData);
            var encodedLength = pcmMonoData.Length / 2;
            var outputBuffer = new byte[encodedLength];
            var length = codec.Encode(state, outputBuffer, wb.ShortBuffer, pcmMonoData.Length / 2);

            if (length != outputBuffer.Length)
            {
                var outputBuffer2 = new byte[length];
                Buffer.BlockCopy(outputBuffer, 0, outputBuffer2, 0, length);
                outputBuffer = outputBuffer2;
            }

            return outputBuffer;
        }

        private static void WriteWavFile(string wavFilename, byte[] audioData, int bitRate)
        {
            var waveFormat = new WaveFormat(bitRate, 16, 1);
            using (var waveFileWriter = new WaveFileWriter(wavFilename, waveFormat))
            {
                waveFileWriter.Write(audioData, 0, audioData.Length);
            }
        }

        private static void WriteG722File(string g722OuputFile, byte[] g722Data)
        {
            using (var file = new FileStream(g722OuputFile, FileMode.Create))
            {
                file.Write(g722Data, 0, g722Data.Length);
            }
        }
    }
}
