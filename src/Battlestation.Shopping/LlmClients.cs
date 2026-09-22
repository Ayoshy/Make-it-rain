using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Battlestation.Shopping;

/// <summary>Un modèle de langage local ou distant. Aucune clé n'est écrite dans un fichier.</summary>
public interface ILlmClient
{
    string Name{get;}
    Task<string> CompleteAsync(string system,string user,CancellationToken cancellation);
}

/// <summary>Ollama reste un service local séparé : le bureau ne fait pas tourner le modèle.</summary>
public sealed class OllamaClient(HttpClient http,string endpoint,string model):ILlmClient
{
    public string Name=>$"Ollama · {model}";

    public async Task<string> CompleteAsync(string system,string user,CancellationToken cancellation)
    {
        var payload=JsonSerializer.Serialize(new
        {
            model,
            stream=false,
            format="json",
            options=new{temperature=0},
            messages=new[]{new{role="system",content=system},new{role="user",content=user}}
        });
        using var request=new HttpRequestMessage(HttpMethod.Post,$"{endpoint.TrimEnd('/')}/api/chat")
        {
            Content=new StringContent(payload,Encoding.UTF8,"application/json")
        };
        using var deadline=LlmJson.Deadline(cancellation);
        using var response=await http.SendAsync(request,HttpCompletionOption.ResponseContentRead,deadline.Token);
        if(!response.IsSuccessStatusCode)throw new InvalidOperationException($"Ollama a répondu {(int)response.StatusCode}.");
        using var document=JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellation));
        return document.RootElement.TryGetProperty("message",out var message)&&message.TryGetProperty("content",out var content)
            ?content.GetString()??""
            :throw new InvalidOperationException("Réponse Ollama sans contenu.");
    }
}

/// <summary>Analyse de la demande avec DeepSeek, sans conserver la clé dans les réglages.</summary>
public sealed class DeepSeekClient(HttpClient http,string apiKey,string model):ILlmClient
{
    public const string Endpoint="https://api.deepseek.com/chat/completions";
    public const string DefaultModel="deepseek-flash";
    public const string KeyVariable="DEEPSEEK_API_KEY";
    public string Name=>$"DeepSeek · {model} · réflexion max";

    /// <summary>La clé vient de l'environnement utilisateur ; elle n'entre ni dans Git ni dans shopping.json.</summary>
    public static string? ApiKey()
    {
        var value=Environment.GetEnvironmentVariable(KeyVariable);
        if(string.IsNullOrWhiteSpace(value))value=Environment.GetEnvironmentVariable(KeyVariable,EnvironmentVariableTarget.User);
        return string.IsNullOrWhiteSpace(value)?null:value.Trim();
    }

    /// <summary>Construit le client distant, ou rien si la clé est absente : l'appelant garde alors le parseur local.</summary>
    public static ILlmClient? Create(HttpClient http,ShoppingSettings settings)
    {
        var key=ApiKey();
        return key is null?null:new DeepSeekClient(http,key,settings.Validate().Model);
    }

    public async Task<string> CompleteAsync(string system,string user,CancellationToken cancellation)
    {
        var payload=JsonSerializer.Serialize(new
        {
            model,
            thinking=new{type="enabled"},
            reasoning_effort="max",
            // Le plafond par défaut du mode max inclut le raisonnement. L'ancien
            // plafond du petit JSON final pouvait couper avant la réponse.
            response_format=new{type="json_object"},
            messages=new[]{new{role="system",content=system},new{role="user",content=user}}
        });
        using var request=new HttpRequestMessage(HttpMethod.Post,Endpoint)
        {
            Content=new StringContent(payload,Encoding.UTF8,"application/json")
        };
        request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",apiKey);
        using var deadline=LlmJson.Deadline(cancellation,180);
        using var response=await http.SendAsync(request,HttpCompletionOption.ResponseContentRead,deadline.Token);
        if(response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
            throw new InvalidOperationException("Clé DeepSeek refusée.");
        if(!response.IsSuccessStatusCode)throw new InvalidOperationException($"DeepSeek a répondu {(int)response.StatusCode}.");
        using var document=JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellation));
        if(!document.RootElement.TryGetProperty("choices",out var choices)||choices.GetArrayLength()==0)
            throw new InvalidOperationException("Réponse DeepSeek sans choix.");
        if(choices[0].TryGetProperty("finish_reason",out var finish)&&finish.GetString()=="length")
            throw new InvalidOperationException("Réponse DeepSeek interrompue : limite de génération atteinte.");
        return choices[0].TryGetProperty("message",out var message)&&message.TryGetProperty("content",out var content)
            ?content.GetString()??""
            :throw new InvalidOperationException("Réponse DeepSeek sans contenu.");
    }

}

/// <summary>Extraction du premier objet JSON d'une réponse de modèle, balises de code comprises.</summary>
public static class LlmJson
{
    /// <summary>Un modèle local peut être lent : la demande est bornée pour ne pas figer le dock.</summary>
    public static CancellationTokenSource Deadline(CancellationToken cancellation,int seconds=45)
    {
        var source=CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        source.CancelAfter(TimeSpan.FromSeconds(seconds));
        return source;
    }

    static readonly JsonSerializerOptions Options=new()
    {
        PropertyNameCaseInsensitive=true,
        AllowTrailingCommas=true,
        ReadCommentHandling=JsonCommentHandling.Skip,
        NumberHandling=JsonNumberHandling.AllowReadingFromString
    };

    public static T? Extract<T>(string text)
    {
        var json=Object(text);
        if(json is null)return default;
        try{return JsonSerializer.Deserialize<T>(json,Options);}catch(JsonException){return default;}
    }

    public static string? Object(string text)
    {
        if(string.IsNullOrWhiteSpace(text))return null;
        int start=text.IndexOf('{'),end=text.LastIndexOf('}');
        return start<0||end<=start?null:text[start..(end+1)];
    }
}
