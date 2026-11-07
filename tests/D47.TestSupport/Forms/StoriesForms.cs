using D47.Core.Audio;

namespace D47.Core.Stories;

public static class StoryForms
{
    extension(CastVoice)
    {
        public static Func<AudioClip, AudioClip>? Treatment(StorySpeaker speaker)
        {
            ArgumentNullException.ThrowIfNull(speaker);

            return CastVoice.Treatment(new PinnedVoice(string.Empty, string.Empty) { Link = speaker.Link, Effects = speaker.Effects });
        }
    }

    extension(StoryCatalog catalog)
    {
        public IReadOnlyList<string> Faults()
        {
            var faults = new List<string>();

            foreach (var card in catalog.Cards)
            {
                if (catalog.Secret(card.Id) is { } hidden)
                {
                    faults.AddRange(StoryCatalog.Faults(card, hidden));
                }
                else
                {
                    faults.Add($"{card.Id}: there is no hidden entry.");
                }
            }

            faults.AddRange(catalog.Secrets.Where(secret => catalog.Find(secret.Id) is null).Select(secret => $"{secret.Id}: the hidden entry has no card."));

            return faults;
        }
    }

    extension(StoryFilter filter)
    {
        public bool Matches(StoryCard card) => filter.Matches(card, StoryRatings.Empty);
    }
}
