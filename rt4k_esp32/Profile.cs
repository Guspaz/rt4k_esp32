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

            // Partially unroll loop
            int dataLen = data.Length;
            int curbyte;

            for (int i = RT4K_PROFILE_HEADER_SIZE; i < dataLen; i += 4)
            {
                curbyte = data[i];
                crc = crc_table[((crc >> 12) ^ (curbyte >> 4)) & 0x0F] ^ (crc << 4);
                crc = crc_table[((crc >> 12) ^ (curbyte)) & 0x0F] ^ (crc << 4);

                curbyte = data[i+1];
                crc = crc_table[((crc >> 12) ^ (curbyte >> 4)) & 0x0F] ^ (crc << 4);
                crc = crc_table[((crc >> 12) ^ (curbyte)) & 0x0F] ^ (crc << 4);

                curbyte = data[i+2];
                crc = crc_table[((crc >> 12) ^ (curbyte >> 4)) & 0x0F] ^ (crc << 4);
                crc = crc_table[((crc >> 12) ^ (curbyte)) & 0x0F] ^ (crc << 4);

                curbyte = data[i+3];
                crc = crc_table[((crc >> 12) ^ (curbyte >> 4)) & 0x0F] ^ (crc << 4);
                crc = crc_table[((crc >> 12) ^ (curbyte)) & 0x0F] ^ (crc << 4);
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
