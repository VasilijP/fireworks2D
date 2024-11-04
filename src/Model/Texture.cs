using Fireworks2D.Util;
using tgalib_core;

namespace Fireworks2D.Model;

public class Texture : IImage
{
    // file name
    public string Name { get; set; }
    
    // is loaded flag
    public bool Loaded { get; set; }
    
    // data [x, y] @ 32 bit RGBA
    public uint[] Data;
    public readonly List<uint[]> Variant = [];
    public readonly Dictionary<string, uint[]> MipMap = new(); // width_height_variant -> data
    public int Width;
    int IImage.Width => Width;
    public int Height;
    int IImage.Height => Height;
    
    public static Texture CreateBlank(int width, int height, uint color)
    {
        Texture texture = new()
        {
            Name = "blank_"+Guid.NewGuid()+".tga",
            Loaded = true, 
            Data = new uint[width * height],
            Width = width,
            Height = height
        };
        
        Span<uint> s = new(texture.Data, 0, width * height);
        s.Fill(color);
        
        return texture;
    }
    
    public void LoadData()
    {
        if (Data == null)
        {
            TgaImage bitmap = new(Name);
            Width = bitmap.Width;
            Height = bitmap.Height;

            // Convert the bitmap to a uint array
            Data = new uint[bitmap.Width * bitmap.Height];
            int index = 0;

            for (int x = 0; x < bitmap.Width; x++) {
                for (int y = 0; y < bitmap.Height; y++) // Store by y to form contiguous data (columns)
                {
                    bitmap.GetPixelRgba(x, Height - y - 1, out int r, out int g, out int b, out int a); // Flipped y-axis from TGA to texture coordinates
                    Data[index++] = Func.EncodePixelColorRgba(r, g, b, a);
                }
            }
        }

        // Load all variants of this texture
        foreach (int variantNumber in GetVariants())
        {
            if (variantNumber < Variant.Count) continue; // Only load if not already loaded
            string variantFileName = $"{Path.GetFileNameWithoutExtension(Name)}_{variantNumber:0000}{Path.GetExtension(Name)}";
            TgaImage bitmapV = new(variantFileName);

            if (bitmapV.Width != Width || bitmapV.Height != Height) { throw new Exception($"Variant {variantNumber} has different size than original texture!"); }

            uint[] variantData = new uint[bitmapV.Width * bitmapV.Height];
            int variantIndex = 0;

            for (int x = 0; x < bitmapV.Width; x++) {
                for (int y = 0; y < bitmapV.Height; y++) // Store by y to form contiguous data for columns
                {
                    bitmapV.GetPixelRgba(x, Height - y - 1, out int r, out int g, out int b, out int a); // Flipped y-axis from TGA to texture coordinates
                    variantData[variantIndex++] = Func.EncodePixelColorRgba(r, g, b, a);
                }
            }

            Variant.Add(variantData);
        }
        
        Loaded = true;
    }
    
    // this will list all available variants of this texture by suffix number, e.g. texture.tga -> texture_0001.tga, texture_0002.tga, etc.
    public IEnumerable<int> GetVariants()
    {
        string fileName = Path.GetFileNameWithoutExtension(Name);
        string extension = Path.GetExtension(Name);
        int variantNumber = 0;
        while (true)
        {
            string variantSuffixFormatted = variantNumber.ToString("0000");
            string variantFileName = $"{fileName}_{variantSuffixFormatted}{extension}";
            if (!File.Exists(variantFileName)) { yield break; }
            yield return variantNumber;
            variantNumber++;
        }
    }

    // save data to file, returns true if successful
    public bool SaveVariant(int variantNumber = -1, bool overwrite = false)
    {
        if (variantNumber == -1) { variantNumber = Variant.Count; } // add new variant

        string name = Path.GetFileNameWithoutExtension(Name);
        string variantSuffixFormatted = variantNumber.ToString("0000");
        string fileName = $"{name}_{variantSuffixFormatted}.tga";
        if (File.Exists(fileName) && !overwrite) { return false; }
        
        using FileStream stream = new FileStream(fileName, FileMode.Create);
        TgaFileFormat.CommonSave(TgaMode.Rgb24Rle, stream, this);

        // Add copy of current texture to variant list
        uint[] variantData = new uint[Data.Length];
        Array.Copy(Data, variantData, Data.Length);
        Variant.Add(variantData);
        return true;
    }

    // For the purpose of saving in TGA format, Y coordinate is flipped
    void IImage.GetPixelRgba(int x, int y, out int r, out int g, out int b, out int a)
    {
        a = 0xFF; // Assume full opacity
        uint color = Data[x * Height + (Height - y - 1)];
        Func.DecodePixelColor(color, out r, out g, out b);
    }
}
