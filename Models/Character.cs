using Avalonia.Media.Imaging;

namespace Eve_Settings_Management.Models
{
    public class Character
    {
        public string? CharacterName { get; set; }

        public string? CharacterId { get; set; }

        public string? CharacterFilePath { get; set; }

        public Bitmap? CharacterPotrait { get; set; }
    }
}