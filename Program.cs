using System.Globalization;

namespace Painting64 {
  enum AreaVibeCheck {
    Good,

    Bad_areaBaseRomAddress,
    Bad_usuallyZero,
    Bad_usually2DCamBit,

    Bad_painting_segmentedAddress_SegmentUnsupported,
    Bad_painting_segmentedAddress,
    Bad_painting_textureSegmentedAddress,
    Bad_painting_textureSegmentedAddress2,
  }

  class Painting {
    public uint romAddress;
    public uint segmentedAddress;
    public uint textureSegmentedAddress;
    public uint textureSegmentedAddress2;
    public bool flipTextures;

    public byte levelID;
    public byte areaID;

    public float pitch;
    public float yaw;

    public float posX;
    public float posY;
    public float posZ;

    public byte alpha = 0xFF;
    public float size = 614.0f;

    public byte[] Binary { get; private set; }

    void AddFloat(byte[] binary, float f, int dest) {
      byte[] bytes = MainClass.GetF32Bytes(f);
      Array.Copy(bytes, 0, binary, dest, bytes.Length);
    }
    void AddU16(byte[] binary, ushort u, int dest) {
      byte[] bytes = MainClass.GetU16Bytes(u);
      Array.Copy(bytes, 0, binary, dest, bytes.Length);
    }
    void AddU32(byte[] binary, uint u, int dest) {
      byte[] bytes = MainClass.GetU32Bytes(u);
      Array.Copy(bytes, 0, binary, dest, bytes.Length);
    }

    void IncrementU32(byte[] binary, uint u, int dest) {
      uint original = (uint)((binary[dest] << 24) + (binary[dest + 1] << 16) + (binary[dest + 2] << 8) + binary[dest + 3]);
      byte[] bytes = MainClass.GetU32Bytes(original + u);
      Array.Copy(bytes, 0, binary, dest, bytes.Length);
    }

    /// Verifies the painting and the area's values
    public AreaVibeCheck IsGoodForArea(uint areaBaseRomAddressStart, uint areaBaseRomAddressEnd, uint usuallyZero, uint usually2DCamBit, long romSize) {
      // On area 15, A3's waterbox system BEEEbytes end up overwriting these unused values in the area table.
      HashSet<uint> usuallyZero_PossibleValues = new() { 0x00000000, 0x42454545 };
      // On area 15, the 'One-0xE-Bank-Per-Area-Code' checkbytes end up overwriting the 2D cam toggle in the area table.
      HashSet<uint> usually2DCamBit_PossibleValues = new() { 0x00000000, 0x00000001, 0x4BC9189A };

      // Verify area itself
      if (areaBaseRomAddressStart == 0 || areaBaseRomAddressStart >= romSize)
        return AreaVibeCheck.Bad_areaBaseRomAddress;
      if (areaBaseRomAddressEnd == 0 || areaBaseRomAddressEnd >= romSize)
        return AreaVibeCheck.Bad_areaBaseRomAddress;
      if (!usuallyZero_PossibleValues.Contains(usuallyZero))
        return AreaVibeCheck.Bad_usuallyZero;
      if (!usually2DCamBit_PossibleValues.Contains(usually2DCamBit))
        return AreaVibeCheck.Bad_usually2DCamBit;

      // Verify painting values

      // this value is used for writing within Painting64, so non-0x0E addresses require explicit support
      if ((segmentedAddress & 0xFF000000) != 0x0E000000)
        return AreaVibeCheck.Bad_painting_segmentedAddress_SegmentUnsupported;

      if ((segmentedAddress & 0xFF000000) == 0x0E000000)
        if (areaBaseRomAddressEnd <= areaBaseRomAddressStart + (segmentedAddress & 0x00FFFFFF))
          return AreaVibeCheck.Bad_painting_segmentedAddress;

      if ((textureSegmentedAddress & 0xFF000000) == 0x0E000000)
        if (areaBaseRomAddressEnd <= areaBaseRomAddressStart + (textureSegmentedAddress & 0x00FFFFFF))
          return AreaVibeCheck.Bad_painting_textureSegmentedAddress;

      if ((textureSegmentedAddress2 & 0xFF000000) == 0x0E000000)
        if (areaBaseRomAddressEnd <= areaBaseRomAddressStart + (textureSegmentedAddress2 & 0x00FFFFFF))
          return AreaVibeCheck.Bad_painting_textureSegmentedAddress2;

      return AreaVibeCheck.Good;
    }

    public void GenerateAndCommit(byte[] samplePaintingBinary, List<Painting> paintings) {
      if (paintings.Count >= MainClass.MAX_PAINTING_COUNT) {
        throw new Exception($"Committing painting: max painting count of {MainClass.MAX_PAINTING_COUNT} exceeded");
      }
      if (romAddress == 0 && levelID == 0) {
        throw new Exception($"Committing painting: level ID / data ROM address not set");
      }
      if (segmentedAddress == 0) {
        throw new Exception($"Committing painting: data segmented address not set");
      }
      if (textureSegmentedAddress == 0) {
        throw new Exception($"Committing painting: texture segmented address not set");
      }

      byte[] binary = new byte[samplePaintingBinary.Length];
      Array.Copy(samplePaintingBinary, binary, binary.Length);

      // Add ID
      AddU16(binary, (ushort)paintings.Count, 0x00);

      AddFloat(binary, pitch, 0x08);
      AddFloat(binary, yaw, 0x0C);
      AddFloat(binary, posX, 0x10);
      AddFloat(binary, posY, 0x14);
      AddFloat(binary, posZ, 0x18);
      AddFloat(binary, size, 0x74);

      binary[0x70] = alpha;

      // Add texture
      if (flipTextures) {
        AddU32(binary, textureSegmentedAddress, 0x78);
        if (textureSegmentedAddress2 == 0) {
          AddU32(binary, textureSegmentedAddress + 0x1000, 0x7C);
        }
        else {
          AddU32(binary, textureSegmentedAddress2, 0x7C);
        }
      }
      else {
        if (textureSegmentedAddress2 == 0) {
          AddU32(binary, textureSegmentedAddress + 0x1000, 0x78);
        }
        else {
          AddU32(binary, textureSegmentedAddress2, 0x78);
        }
        AddU32(binary, textureSegmentedAddress, 0x7C);
      }
      // texture pointer
      AddU32(binary, MainClass.SEG0E_RAM_START + (segmentedAddress & 0x00FFFFFF) + 0x78, 0x60);

      Binary = binary;
      paintings.Add(this);
    }
  }

  class MainClass {
    public const uint SEG0E_RAM_START = 0x80420000;
    public const int PAINTING_ARRAY_START = 0x3DB2BC;
    public const int MAX_PAINTING_COUNT = 256;

    static string samplePaintingBinaryDump = "00000200 00000000 00000000 42B40000 C539F000 42800000 44AA4000 00000000 41A00000 42A00000 3F800000 3F75F6FD 3F73D07D 00000000 3E75C28F 3E0F5C29 00000000 42200000 41F00000 00000000 00000000 00000000 8056BE60 8056BE40 8056FDA0 00400020 8056BE60 0AFF0000 00000000 44198000 0E00E420 0E00D420";

    // N64 is big endian so we need these lol
    public static byte[] GetU16Bytes(ushort u) {
      byte[] bytes = BitConverter.GetBytes(u);
      return new byte[] { bytes[1], bytes[0] };
    }

    public static byte[] GetU32Bytes(uint u) {
      byte[] bytes = BitConverter.GetBytes(u);
      return new byte[] { bytes[3], bytes[2], bytes[1], bytes[0] };
    }

    public static byte[] GetF32Bytes(float f) {
      byte[] bytes = BitConverter.GetBytes(f);
      return new byte[] { bytes[3], bytes[2], bytes[1], bytes[0] };
    }


    static uint U32FromBytes(byte[] binary) {
      return (uint)((binary[0] << 24) + (binary[1] << 16) + (binary[2] << 8) + binary[3]);
    }

    static void _Main(string[] args) {
      CultureInfo cult = CultureInfo.GetCultureInfo("en-US");
      Thread.CurrentThread.CurrentCulture = cult;
      Thread.CurrentThread.CurrentUICulture = cult;

      samplePaintingBinaryDump = samplePaintingBinaryDump.Replace(" ", "");
      byte[] samplePaintingBinary = new byte[samplePaintingBinaryDump.Length / 2];
      for (int i = 0; i < samplePaintingBinaryDump.Length; i += 2) {
        byte b = Convert.ToByte(samplePaintingBinaryDump.Substring(i, 2), 16);
        samplePaintingBinary[i / 2] = b;
      }
      Console.WriteLine("Loaded sample painting binary!");

      if (!File.Exists("paintingcfg.txt")) {
        Console.WriteLine("paintingcfg.txt does not exist!");
        return;
      }

      StreamReader sr = new StreamReader("paintingcfg.txt");
      List<Painting> paintings = new List<Painting>();
      Painting currentPainting = null;
      int linen = 0;
      uint baseRomAddress = 0;
      uint baseSegmentedAddress = 0;
      byte levelID = 0;
      byte areaID = 0;

      try {
        while (!sr.EndOfStream) {
          string ln = sr.ReadLine();
          linen++;

          string key = ln.Split('=')[0].ToLowerInvariant();
          if (key == "new_painting") {
            if (currentPainting != null) {
              currentPainting.GenerateAndCommit(samplePaintingBinary, paintings);
            }
            currentPainting = new Painting();
            currentPainting.romAddress = baseRomAddress;
            currentPainting.segmentedAddress = baseSegmentedAddress;
            currentPainting.levelID = levelID;
            currentPainting.areaID = areaID;
            baseRomAddress += 0x80;
            baseSegmentedAddress += 0x80;
          }
          else if (ln.Length > key.Length + 1) {
            string strValue = ln.Substring(key.Length + 1);
            string value = strValue;

            if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) {
              try {
                ulong u = Convert.ToUInt64(value.Substring(2), 16);
                value = u.ToString();
              }
              catch { }
            }

            switch (key) {
              case "level_id":
                levelID = byte.Parse(value);
                break;
              case "area_id":
                areaID = byte.Parse(value);
                break;
              case "base_rom_address":
                baseRomAddress = uint.Parse(value);
                break;
              case "base_segmented_address":
                baseSegmentedAddress = uint.Parse(value);
                break;
              case "rom_address":
                currentPainting.romAddress = uint.Parse(value);
                break;
              case "segmented_address":
                currentPainting.segmentedAddress = uint.Parse(value);
                break;
              case "texture_segmented_address":
                currentPainting.textureSegmentedAddress = uint.Parse(value);
                break;
              case "texture_segmented_address_half2":
                currentPainting.textureSegmentedAddress2 = uint.Parse(value);
                break;
              case "rotation":
                currentPainting.yaw = float.Parse(value, cult);
                break;
              case "x":
              case "posx":
              case "xpos":
              case "pos_x":
              case "x_pos":
                currentPainting.posX = float.Parse(value, cult);
                break;
              case "y":
              case "posy":
              case "ypos":
              case "pos_y":
              case "y_pos":
                currentPainting.posY = float.Parse(value, cult);
                break;
              case "z":
              case "posz":
              case "zpos":
              case "pos_z":
              case "z_pos":
                currentPainting.posZ = float.Parse(value, cult);
                break;
              case "size":
              case "scale":
                currentPainting.size = float.Parse(value, cult);
                break;
              case "alpha":
                currentPainting.alpha = byte.Parse(value);
                break;
              case "flip":
                currentPainting.flipTextures = true;
                break;
            }
          }
        }

        if (currentPainting != null) {
          currentPainting.GenerateAndCommit(samplePaintingBinary, paintings);
        }

        sr.Close();
      }
      catch (Exception e) {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"Exception at line {linen} in paintingcfg.txt:\n\n{e}");
        sr.Close();
        Console.ReadKey(true);
        return;
      }

      string romFile = null;
      if (args.Length == 0) {
        Console.WriteLine($"Enter filename of ROM file (or drag it onto the exe next time)");
        romFile = Console.ReadLine();
      }
      else {
        romFile = args[0];
      }

      if (!args.Contains("--automatic")) {
        Console.Write($"Write {paintings.Count} paintings to {romFile}. Is this okay? (Y/N)");
        while (true) {
          char c = Console.ReadKey(true).KeyChar;
          if (c == 'n' || c == 'N') {
            Console.WriteLine("\nok bye lmao");
            return;
          }

          if (c == 'y' || c == 'Y') {
            break;
          }
        }
        Console.WriteLine();
      }

      if (!File.Exists(romFile)) {
        Console.WriteLine($"Provided ROM file {romFile} does not exist!");
      }

      // idk if this many levels can even exist but who knows lol
      uint[] levelStartAddresses = new uint[0x100];
      BinaryReader reader = new BinaryReader(File.OpenRead(romFile));
      long romSize = reader.BaseStream.Length;
      int _i = 0;
      bool validLevel = true;
      while (validLevel) {
        reader.BaseStream.Seek(0x2ABF20 + _i++ * 0x0C, SeekOrigin.Begin);
        uint levelId = U32FromBytes(reader.ReadBytes(4));
        uint segmentedAddress = U32FromBytes(reader.ReadBytes(4));

        validLevel &= levelId < 0x100 && (segmentedAddress & 0xFF000000) == 0x15000000;

        if (validLevel) {
          reader.BaseStream.Seek(0x2ABCA0 + (segmentedAddress & 0x00FFFFFF) + 0x04, SeekOrigin.Begin);
          levelStartAddresses[levelId] = U32FromBytes(reader.ReadBytes(4));
        }
      }

      // extract ROM addresses from level+area IDs for the paintings
      _i = -1;
      foreach (Painting painting in paintings) {
        _i++;
        if (painting.levelID != 0) {
          uint baseAddress = levelStartAddresses[painting.levelID];

          if (baseAddress == 0) {
            Console.WriteLine($"Level {painting.levelID} does not exist??");
            Console.ReadKey(true);
            return;
          }

          reader.BaseStream.Seek(baseAddress + 0x5F00 + painting.areaID * 0x10, SeekOrigin.Begin);
          uint areaBaseRomAddressStart = U32FromBytes(reader.ReadBytes(4));
          uint areaBaseRomAddressEnd = U32FromBytes(reader.ReadBytes(4));
          uint usuallyZero = U32FromBytes(reader.ReadBytes(4));
          uint usually2DCamBit = U32FromBytes(reader.ReadBytes(4));

          AreaVibeCheck result = painting.IsGoodForArea(areaBaseRomAddressStart, areaBaseRomAddressEnd, usuallyZero, usually2DCamBit, romSize);
          if (result != AreaVibeCheck.Good) {
            switch (result) {
              case AreaVibeCheck.Bad_areaBaseRomAddress:
                Console.WriteLine($"Validation of area {painting.areaID} in level {painting.levelID} failed:");
                Console.WriteLine("no area, or ROM addresses are corrupted");
                Console.WriteLine("(This currently only works on ROM manger areas which have an area table in segment 19)");
                break;
              case AreaVibeCheck.Bad_usuallyZero:
                Console.WriteLine($"Validation of area {painting.areaID} in level {painting.levelID} failed:");
                Console.WriteLine("Wrong 'usuallyZero' value");
                Console.WriteLine("(This currently only works on ROM manger areas which have an area table in segment 19)");
                break;
              case AreaVibeCheck.Bad_usually2DCamBit:
                Console.WriteLine($"Validation of area {painting.areaID} in level {painting.levelID} failed:");
                Console.WriteLine("Wrong 'usually2DCamBit' value");
                Console.WriteLine("(This currently only works on ROM manger areas which have an area table in segment 19)");
                break;
              case AreaVibeCheck.Bad_painting_segmentedAddress_SegmentUnsupported:
                Console.WriteLine($"Validation of painting {_i} failed for area {painting.areaID} in level {painting.levelID}:");
                Console.WriteLine("segmentedAddress must be in segment 0x0E (other segments require explicit support in Painting64)");
                break;
              case AreaVibeCheck.Bad_painting_segmentedAddress:
                Console.WriteLine($"Validation of painting {_i} failed for area {painting.areaID} in level {painting.levelID}:");
                Console.WriteLine("segmentedAddress escaped the area");
                break;
              case AreaVibeCheck.Bad_painting_textureSegmentedAddress:
                Console.WriteLine($"Validation of painting {_i} failed for area {painting.areaID} in level {painting.levelID}:");
                Console.WriteLine("textureSegmentedAddress escaped the area");
                break;
              case AreaVibeCheck.Bad_painting_textureSegmentedAddress2:
                Console.WriteLine($"Validation of painting {_i} failed for area {painting.areaID} in level {painting.levelID}:");
                Console.WriteLine("textureSegmentedAddress2 escaped the area");
                break;
              default:
                Console.WriteLine($"Validation of painting {_i} failed for area {painting.areaID} in level {painting.levelID}:");
                Console.WriteLine("unknown reason! (please meow at Chlorobyte for the rest of time for not explaining the reason)");
                break;
            }

            Console.ReadKey(true);
            return;
          }

          painting.romAddress = areaBaseRomAddressStart + (painting.segmentedAddress & 0x00FFFFFF);
        }
      }

      // ok we are done with reading rome
      reader.Close();


      // let's start writing paintings
      BinaryWriter writer = new BinaryWriter(File.OpenWrite(romFile));
      foreach (Painting painting in paintings) {
        writer.Seek((int)painting.romAddress, SeekOrigin.Begin);
        writer.Write(painting.Binary);
      }
      writer.Seek(PAINTING_ARRAY_START, SeekOrigin.Begin);
      foreach (Painting painting in paintings) {
        writer.Write(GetU32Bytes(SEG0E_RAM_START + (painting.segmentedAddress & 0x00FFFFFF)));
      }
      writer.Write(GetU32Bytes(0));
      writer.Close();

      Console.WriteLine($"Paintings successfully written to {romFile}!");
    }

    public static void Main(string[] args) {
      try {
        _Main(args);
      }
      catch (Exception e) {
        Console.WriteLine("fatal error\n" + e);
      }

      if (!args.Contains("--automatic")) {
        Console.WriteLine("Press any key to exit");
        Console.ReadKey(true);
      }
    }
  }
}

