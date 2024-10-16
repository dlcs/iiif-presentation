using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mapper.DlcsApi
{
    /// <summary>
    /// In this demo this is a stand-in for DLCS Hydra Image (Asset)
    /// </summary>
    public class Asset
    {
        public required string Id { get; set; }
        public required string MediaType { get; set; }
        public required string Origin { get; set; }
    }
}
