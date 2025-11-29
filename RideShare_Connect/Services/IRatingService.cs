using RideShare_Connect.DTOs;
using System.Threading.Tasks;

namespace RideShare_Connect.Services
{
    public interface IRatingService
    {
        Task<DriverRatingResponseDto> CreateAsync(int passengerId, DriverRatingCreateDto dto);
    }
}
