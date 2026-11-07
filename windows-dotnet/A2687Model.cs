namespace Charker;
internal static class A2687Model {
 // ASHE is used by the user's chargers. ASHD is also present in the upstream
 // captured A2687 base-info fixture; a shared FF09 service alone is not a model ID.
 public static bool ChargerSerial(string? serial)=>serial?.StartsWith("ASHE",StringComparison.OrdinalIgnoreCase)==true||serial?.StartsWith("ASHD",StringComparison.OrdinalIgnoreCase)==true;
 public static bool OtherSerial(string? serial)=>!string.IsNullOrWhiteSpace(serial)&&!ChargerSerial(serial);
 public static bool Advertisement(string name,IEnumerable<byte[]> productData)=>name.Contains("A2687",StringComparison.OrdinalIgnoreCase)||(name.StartsWith("ASHE",StringComparison.OrdinalIgnoreCase)&&name.Length>=8)||productData.Any(bytes=>bytes.Zip(bytes.Skip(1)).Any(p=>p.First==0xb4&&p.Second==0x05));
}
