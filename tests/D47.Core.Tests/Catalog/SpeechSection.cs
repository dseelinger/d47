namespace D47.Core.Tests.Catalog;

/// <summary>A catalog's <c>speech</c> section, for tests that write a catalog of their own.</summary>
internal static class SpeechSection
{
    public static string Json(
        string elevenLabsDefault = "eleven_v4_turbo",
        string v4TurboPrice = "0.04",
        string v3Price = "0.04") => $$"""
        "speech": {
          "elevenlabs": {
            "default": "{{elevenLabsDefault}}",
            "models": [
              { "id": "eleven_v4_turbo", "label": "v4 Turbo", "offered": true, "dollarsPerThousandCharacters": {{v4TurboPrice}}, "readsTags": true, "groupsSentencesUpTo": 300 },
              { "id": "eleven_v3_conversational", "label": "v3", "offered": true, "dollarsPerThousandCharacters": {{v3Price}}, "readsTags": true, "groupsSentencesUpTo": 300 },
              { "id": "eleven_flash_v2_5", "label": "Flash 2.5", "offered": false, "readsRate": true }
            ]
          },
          "openai": { "default": "tts-a", "models": [ { "id": "tts-a", "offered": true } ] },
          "cartesia": { "default": "sonic-a", "models": [ { "id": "sonic-a", "offered": true } ] }
        }
        """;
}
