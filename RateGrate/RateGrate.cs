using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RateGrate
{
    /// <summary>
    /// A RateGrate that will allow a specified number of queries during any rate limit period of specified 
    /// </summary>
    public abstract class RateGrate
    {
        /// <summary>
        /// Waits for an api query slot to open up according to this grate's rate limit definition
        /// @todo change this dumbass name
        /// </summary>
        public abstract void GrateWait();
    }
}
