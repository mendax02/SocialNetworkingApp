using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using AutoMapper;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using DatingApp.API.Data;
using DatingApp.API.Dtos;
using DatingApp.API.Helpers;
using DatingApp.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace DatingApp.API.Controllers
{
    [Authorize]
    [Route("api/users/{userId}/photos")]
    [ApiController]
    public class PhotosController : ControllerBase
    {
        private readonly IDatingRepository _repo;
        private readonly IMapper _mapper;
        private readonly IOptions<CloudinarySettings> _cloudinaryConfig;
        private Cloudinary _cloudinary;

        public PhotosController(IDatingRepository repo, IMapper mapper,        
        IOptions<CloudinarySettings> cloudinaryConfig)
        {
            this._cloudinaryConfig = cloudinaryConfig;
            this._mapper = mapper;
            this._repo = repo;

            Account acc = new Account(
                    _cloudinaryConfig.Value.CloudName,
                    _cloudinaryConfig.Value.ApiKey,
                    _cloudinaryConfig.Value.ApiSecret
            );

            _cloudinary = new Cloudinary(acc);
        }

        [HttpGet("{id}", Name = "GetPhoto")]
        public async Task<IActionResult> GetPhoto(int id)
        {
            var photosFromRepo = await _repo.GetPhoto(id);
            
            var photo =  _mapper.Map<PhotoForReturnDto>(photosFromRepo);

            return Ok(photo);
        }

        [HttpPost]
        public async Task<IActionResult> AddPhotoForUser(int userId,
            [FromForm]PhotoForCreationDto photoForCreationDto)
        {
             if (userId != int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value))
                 return Unauthorized();

            var userFromRepo = await _repo.GetUser(userId);

            var file = photoForCreationDto.File;

            var uploadResult = new ImageUploadResult();

            if (file.Length > 0) 
            {
                using( var stream = file.OpenReadStream())
                {
                    var uploadParams = new ImageUploadParams()
                    {
                        File = new FileDescription(file.Name, stream),
                        Transformation = new Transformation().Width(500).Height(500).Crop("fill")
                        .Gravity("face")
                    };
                    uploadResult = _cloudinary.Upload(uploadParams);
                }
            }

            photoForCreationDto.Url = uploadResult.Uri.ToString();
            photoForCreationDto.PublicId = uploadResult.PublicId;

            var photo = _mapper.Map<Photo>(photoForCreationDto);

            if (!userFromRepo.Photos.Any(user=> user.IsMain))
            {
                photo.IsMain = true;
            }

            userFromRepo.Photos.Add(photo);

                if(await _repo.SaveAll())
                {
                     var photoToReturn = _mapper.Map<PhotoForReturnDto>(photo);
                    return CreatedAtRoute("GetPhoto", new { id = photo.Id}, photoToReturn);
                }

                return BadRequest("Could not upload the photo");
        }

        [HttpPost("{id}/setMain")]
        public async Task<IActionResult> SetMainPhoto( int userId, int id) 
        {
            if (userId != int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value))
                 return Unauthorized();

            var user = await _repo.GetUser(userId);

            if(!user.Photos.Any(p => p.Id == id))
            {
                return Unauthorized();
            }    

            var photosFromRepo = await _repo.GetPhoto(id);

            if(photosFromRepo.IsMain)
            {
                return BadRequest("This is already the main photo");
            }

            var currentMainPhoto = await _repo.GetMainPhotoForUser(userId);
            currentMainPhoto.IsMain = false;

            photosFromRepo.IsMain = true;

            if(await _repo.SaveAll())
                return NoContent();

            return BadRequest("Could not set photo to main");
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeletePhoto(int userId, int id)
        {
             if (userId != int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value))
                 return Unauthorized();

            var user = await _repo.GetUser(userId);

            if(!user.Photos.Any(p => p.Id == id))
            {
                return Unauthorized();
            }    

            var photosFromRepo = await _repo.GetPhoto(id);

            if(photosFromRepo.IsMain)
            {
                return BadRequest("You cannot delete Main photo!!");
            }

            if(photosFromRepo.PublicId != null)
            {
                var deleteParams = new DeletionParams(photosFromRepo.PublicId);

                var result = _cloudinary.Destroy(deleteParams);

                if(result.Result == "ok")
                {
                    _repo.Delete(photosFromRepo);
                }
            }

            if(photosFromRepo.PublicId == null)
            {
                _repo.Delete(photosFromRepo);   
            }

            if(await _repo.SaveAll())
                return Ok();
            
            return BadRequest("Failed to delete the Photo");
        }
    }
}