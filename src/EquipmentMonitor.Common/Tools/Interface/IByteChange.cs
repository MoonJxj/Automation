using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common
{
    public interface IByteChange
    {
        ushort BoolToUshort(bool[] array);

        ushort ByteToUshort(byte[] array);

        bool[] UshortToBool(ushort value, int length);
    }
}
