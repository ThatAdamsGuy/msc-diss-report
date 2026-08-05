using System.Collections.Generic;
using System.Threading.Tasks;
using UndercutAnalyser.Domain.Models;

namespace UndercutAnalyser.Services
{
    public interface IEventDataProvider
    {
        /// <summary>
        /// Get races for the specified season (year).
        /// </summary>
        /// <param name="year">Season year (e.g. 2023)</param>
        /// <returns>List of races in that season</returns>
        Task<IReadOnlyList<EventRace>> GetRacesBySeasonAsync(int year);
    }
}
