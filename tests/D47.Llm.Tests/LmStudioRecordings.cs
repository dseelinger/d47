namespace D47.Llm.Tests;

/// <summary>LM Studio 0.4.25.0's own responses to its REST API, captured with E4B on this machine.</summary>
internal static class LmStudioRecordings
{
    /// <summary><c>GET /api/v1/models</c> with E4B loaded at 8,192.</summary>
    public const string Loaded =
        """
        {
          "models": [
            {
              "type": "llm",
              "publisher": "google",
              "key": "google/gemma-4-e4b",
              "display_name": "Gemma 4 E4B",
              "architecture": "gemma4",
              "quantization": {
                "name": "Q4_K_M",
                "bits_per_weight": 4
              },
              "size_bytes": 6326918619,
              "params_string": "7.5B",
              "loaded_instances": [
                {
                  "id": "google/gemma-4-e4b",
                  "config": {
                    "context_length": 8192,
                    "eval_batch_size": 2048,
                    "physical_batch_size": 512,
                    "parallel": 4,
                    "flash_attention": true,
                    "context_checkpoints": 32,
                    "reasoning_budget_message": "",
                    "speculative_draft_mtp": false,
                    "speculative_draft_simple": false,
                    "speculative_draft_model": "",
                    "speculative_draft_max_tokens": 3,
                    "speculative_draft_min_tokens": 0,
                    "speculative_draft_min_continue_probability": 0,
                    "offload_kv_cache_to_gpu": true
                  }
                }
              ],
              "max_context_length": 131072,
              "format": "gguf",
              "capabilities": {
                "vision": true,
                "trained_for_tool_use": true,
                "reasoning": {
                  "allowed_options": [
                    "off",
                    "on"
                  ],
                  "default": "on"
                }
              },
              "description": null,
              "variants": [
                "google/gemma-4-e4b@q4_k_m"
              ],
              "selected_variant": "google/gemma-4-e4b@q4_k_m"
            },
            {
              "type": "embedding",
              "publisher": "nomic-ai",
              "key": "text-embedding-nomic-embed-text-v1.5",
              "display_name": "Nomic Embed Text v1.5",
              "quantization": {
                "name": "Q4_K_M",
                "bits_per_weight": 4
              },
              "size_bytes": 84106624,
              "params_string": null,
              "loaded_instances": [],
              "max_context_length": 2048,
              "format": "gguf"
            }
          ]
        }
        """;

    /// <summary><c>GET /api/v1/models</c> with E4B not loaded.</summary>
    public const string NotLoaded =
        """
        {
          "models": [
            {
              "type": "llm",
              "publisher": "google",
              "key": "google/gemma-4-e4b",
              "display_name": "Gemma 4 E4B",
              "architecture": "gemma4",
              "quantization": {
                "name": "Q4_K_M",
                "bits_per_weight": 4
              },
              "size_bytes": 6326918619,
              "params_string": "7.5B",
              "loaded_instances": [],
              "max_context_length": 131072,
              "format": "gguf",
              "capabilities": {
                "vision": true,
                "trained_for_tool_use": true,
                "reasoning": {
                  "allowed_options": [
                    "off",
                    "on"
                  ],
                  "default": "on"
                }
              },
              "description": null,
              "variants": [
                "google/gemma-4-e4b@q4_k_m"
              ],
              "selected_variant": "google/gemma-4-e4b@q4_k_m"
            },
            {
              "type": "embedding",
              "publisher": "nomic-ai",
              "key": "text-embedding-nomic-embed-text-v1.5",
              "display_name": "Nomic Embed Text v1.5",
              "quantization": {
                "name": "Q4_K_M",
                "bits_per_weight": 4
              },
              "size_bytes": 84106624,
              "params_string": null,
              "loaded_instances": [],
              "max_context_length": 2048,
              "format": "gguf"
            }
          ]
        }
        """;

    /// <summary><c>GET /api/v1/models</c> after the unload and a load at 16,384. The instance keeps the key as its id.</summary>
    public const string Reloaded =
        """
        {
          "models": [
            {
              "type": "llm",
              "publisher": "google",
              "key": "google/gemma-4-e4b",
              "display_name": "Gemma 4 E4B",
              "architecture": "gemma4",
              "quantization": {
                "name": "Q4_K_M",
                "bits_per_weight": 4
              },
              "size_bytes": 6326918619,
              "params_string": "7.5B",
              "loaded_instances": [
                {
                  "id": "google/gemma-4-e4b",
                  "config": {
                    "context_length": 16384,
                    "eval_batch_size": 2048,
                    "physical_batch_size": 512,
                    "parallel": 4,
                    "flash_attention": true,
                    "context_checkpoints": 32,
                    "reasoning_budget_message": "",
                    "speculative_draft_mtp": false,
                    "speculative_draft_simple": false,
                    "speculative_draft_model": "",
                    "speculative_draft_max_tokens": 3,
                    "speculative_draft_min_tokens": 0,
                    "speculative_draft_min_continue_probability": 0,
                    "offload_kv_cache_to_gpu": true
                  }
                }
              ],
              "max_context_length": 131072,
              "format": "gguf",
              "capabilities": {
                "vision": true,
                "trained_for_tool_use": true,
                "reasoning": {
                  "allowed_options": [
                    "off",
                    "on"
                  ],
                  "default": "on"
                }
              },
              "description": null,
              "variants": [
                "google/gemma-4-e4b@q4_k_m"
              ],
              "selected_variant": "google/gemma-4-e4b@q4_k_m"
            },
            {
              "type": "embedding",
              "publisher": "nomic-ai",
              "key": "text-embedding-nomic-embed-text-v1.5",
              "display_name": "Nomic Embed Text v1.5",
              "quantization": {
                "name": "Q4_K_M",
                "bits_per_weight": 4
              },
              "size_bytes": 84106624,
              "params_string": null,
              "loaded_instances": [],
              "max_context_length": 2048,
              "format": "gguf"
            }
          ]
        }
        """;

    /// <summary><c>GET /api/v1/models</c> after a load at 4,096 sent while the 8,192 instance was still loaded, which names the new one <c>:2</c>.</summary>
    public const string TwoInstances =
        """
        {
          "models": [
            {
              "type": "llm",
              "publisher": "google",
              "key": "google/gemma-4-e4b",
              "display_name": "Gemma 4 E4B",
              "architecture": "gemma4",
              "quantization": {
                "name": "Q4_K_M",
                "bits_per_weight": 4
              },
              "size_bytes": 6326918619,
              "params_string": "7.5B",
              "loaded_instances": [
                {
                  "id": "google/gemma-4-e4b",
                  "config": {
                    "context_length": 8192,
                    "eval_batch_size": 2048,
                    "physical_batch_size": 512,
                    "parallel": 4,
                    "flash_attention": true,
                    "context_checkpoints": 32,
                    "reasoning_budget_message": "",
                    "speculative_draft_mtp": false,
                    "speculative_draft_simple": false,
                    "speculative_draft_model": "",
                    "speculative_draft_max_tokens": 3,
                    "speculative_draft_min_tokens": 0,
                    "speculative_draft_min_continue_probability": 0,
                    "offload_kv_cache_to_gpu": true
                  }
                },
                {
                  "id": "google/gemma-4-e4b:2",
                  "config": {
                    "context_length": 4096,
                    "eval_batch_size": 2048,
                    "physical_batch_size": 512,
                    "parallel": 4,
                    "flash_attention": true,
                    "context_checkpoints": 32,
                    "reasoning_budget_message": "",
                    "speculative_draft_mtp": false,
                    "speculative_draft_simple": false,
                    "speculative_draft_model": "",
                    "speculative_draft_max_tokens": 3,
                    "speculative_draft_min_tokens": 0,
                    "speculative_draft_min_continue_probability": 0,
                    "offload_kv_cache_to_gpu": true
                  }
                }
              ],
              "max_context_length": 131072,
              "format": "gguf",
              "capabilities": {
                "vision": true,
                "trained_for_tool_use": true,
                "reasoning": {
                  "allowed_options": [
                    "off",
                    "on"
                  ],
                  "default": "on"
                }
              },
              "description": null,
              "variants": [
                "google/gemma-4-e4b@q4_k_m"
              ],
              "selected_variant": "google/gemma-4-e4b@q4_k_m"
            },
            {
              "type": "embedding",
              "publisher": "nomic-ai",
              "key": "text-embedding-nomic-embed-text-v1.5",
              "display_name": "Nomic Embed Text v1.5",
              "quantization": {
                "name": "Q4_K_M",
                "bits_per_weight": 4
              },
              "size_bytes": 84106624,
              "params_string": null,
              "loaded_instances": [],
              "max_context_length": 2048,
              "format": "gguf"
            }
          ]
        }
        """;

    /// <summary><c>POST /api/v1/models/unload</c> for the loaded instance.</summary>
    public const string Unloaded =
        """
        {
          "instance_id": "google/gemma-4-e4b"
        }
        """;

    /// <summary><c>POST /api/v1/models/load</c> at 16,384, sent after the unload.</summary>
    public const string LoadedAt16384 =
        """
        {
          "type": "llm",
          "instance_id": "google/gemma-4-e4b",
          "load_time_seconds": 6.387,
          "status": "loaded"
        }
        """;

    /// <summary><c>POST /api/v1/models/load</c> for a model that is not downloaded, with status 404.</summary>
    public const string ModelNotFound =
        """
        {
          "error": {
            "type": "model_not_found",
            "message": "Model google/no-such-model not found in downloaded models"
          }
        }
        """;

    /// <summary>A 404 for a path LM Studio does not serve.</summary>
    public const string UnknownRoute =
        """
        {"error":"Unexpected endpoint or method. (GET /api/v1/nope)"}
        """;

    /// <summary><c>GET /v1/models</c> with E4B loaded, which lists the key as the id d47 sends.</summary>
    public const string ChatModels =
        """
        {
          "data": [
            {
              "id": "google/gemma-4-e4b",
              "object": "model",
              "owned_by": "organization_owner"
            },
            {
              "id": "text-embedding-nomic-embed-text-v1.5",
              "object": "model",
              "owned_by": "organization_owner"
            }
          ],
          "object": "list"
        }
        """;
}
