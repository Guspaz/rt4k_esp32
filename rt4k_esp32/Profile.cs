using System;

namespace rt4k_esp32
{
    internal class Profile
    {
        private byte[] profileData;

        const int RT4K_PROFILE_CRC_OFFSET = 32;
        const int RT4K_PROFILE_HEADER_SIZE = 128;

        private static int rt_crc(byte[] data)
        {
            int[] crc_table = { 0x0000, 0x1021, 0x2042, 0x3063, 0x4084, 0x50a5, 0x60c6, 0x70e7, 0x8108, 0x9129, 0xa14a, 0xb16b, 0xc18c, 0xd1ad, 0xe1ce, 0xf1ef };
            int crc = 0;

            // This is actually faster than a for loop initialized at the end of the header
            byte[] skippedData = new byte[data.Length - RT4K_PROFILE_HEADER_SIZE];
            Array.Copy(data, RT4K_PROFILE_HEADER_SIZE, skippedData, 0, skippedData.Length);

            foreach (byte b in skippedData)
            {
                crc = crc_table[((crc >> 12) ^ (b >> 4)) & 0x0F] ^ (crc << 4);
                crc = crc_table[((crc >> 12) ^ (b)) & 0x0F] ^ (crc << 4);
            }

            return crc & 0xFFFF;
        }

        public Profile(byte[] data)
        {
            profileData = data;
            // TODO: Parse the profile into the struct
        }

        public void UpdateBytes(int address, byte[] data)
        {
            int i = 0;
            foreach (byte b in data)
            {
                profileData[address + i] = b;
                i++;
            }
        }

        public byte[] Save()
        {
            // Update the CRC
            UpdateBytes(RT4K_PROFILE_CRC_OFFSET, BitConverter.GetBytes((ushort)rt_crc(profileData)));

            // Write the profile to a stream
            return profileData;
        }
    }
}
