using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using MudBlazor;
using System.Net.Http.Json;
using TicketPrime.Web.Models;
using TicketPrime.Web.Validators;

namespace TicketPrime.Web.Components.Pages;

public partial class EventoCreate : IAsyncDisposable
{
    [Inject] private IJSRuntime JS { get; set; } = default!;
    // ─────────────────────────────────────────────────────────────────────────
    // Constantes de negócio
    // ─────────────────────────────────────────────────────────────────────────
    private const int    MaxFotos              = 10;
    private const int    MinFotos              = 1;
    private const long   MaxTamanhoFoto        = 5 * 1024 * 1024;   // 5 MB
    private const int    MinResolucaoLargura   = 800;
    private const int    MinResolucaoAltura    = 600;
    private const int    MaxEventosAtivosOrg   = 5;   // limite por organizador
    private static readonly HashSet<string> TiposAceitos =
        ["image/jpeg", "image/png", "image/webp"];

    // ─────────────────────────────────────────────────────────────────────────
    // Estado do formulário
    // ─────────────────────────────────────────────────────────────────────────
    private EventoCreateDto          _evento    = new();
    private EventoCreateDtoValidator  _validator = new();
    private MudForm?                  _form;
    private bool                     _formValido;

    // ─────────────────────────────────────────────────────────────────────────
    // Estado de fotos
    // ─────────────────────────────────────────────────────────────────────────
    private readonly List<FotoItem>     _fotos           = [];
    /// <summary>Hashes SHA-256 dos conteúdos de imagem já adicionados (deduplicação).</summary>
    private readonly HashSet<string>    _hashesConteudo  = [];

    // ─────────────────────────────────────────────────────────────────────────
    // Estado da UI
    // ─────────────────────────────────────────────────────────────────────────
    private bool    _carregando          = false;
    private bool    _criptografandoFotos = false;
    private bool    _isDragOver          = false;
    private bool    _cryptoInicializado  = false;
    private string? _cryptoErro          = null;

    private DotNetObjectReference<EventoCreate>? _dotNetRef;

    // ─────────────────────────────────────────────────────────────────────────
    // Tema MudBlazor – dark
    // ─────────────────────────────────────────────────────────────────────────
    private readonly MudTheme _tema = new()
    {
        PaletteDark = new PaletteDark
        {
            Primary           = "#7C3AED",
            PrimaryDarken     = "#6D28D9",
            PrimaryLighten    = "#8B5CF6",
            Secondary         = "#5B5BD6",
            Tertiary          = "#0EA5E9",
            Background        = "#0D1117",
            BackgroundGray    = "#161B22",
            Surface           = "#161B22",
            DrawerBackground  = "#0D1117",
            AppbarBackground  = "#0D1117",
            TextPrimary       = "#E6EDF3",
            TextSecondary     = "#8B949E",
            ActionDefault     = "#8B949E",
            ActionDisabled    = "#30363D",
            ActionDisabledBackground = "#21262D",
            Divider           = "#30363D",
            LinesDefault      = "#30363D",
            TableLines        = "#21262D",
            Info              = "#58A6FF",
            Success           = "#3FB950",
            Warning           = "#D29922",
            Error             = "#F85149",
        }
    };

    // ─────────────────────────────────────────────────────────────────────────
    // Gêneros musicais disponíveis
    // ─────────────────────────────────────────────────────────────────────────
    private static readonly string[] _generosMusicas =
        ["Rock", "Pop", "Sertanejo", "Eletrônico", "Forró", "MPB", "Outro"];

    // Campos auxiliares para separar data e hora antes de combinar em DataHora
    private DateTime? _dataEvento;
    private TimeSpan? _horaEvento;

    private void OnDataChanged(DateTime? val)
    {
        _dataEvento = val;
        AtualizarDataHora();
    }

    private void OnHoraChanged(TimeSpan? val)
    {
        _horaEvento = val;
        AtualizarDataHora();
    }

    private void AtualizarDataHora()
    {
        if (_dataEvento.HasValue)
            _evento.DataHora = _dataEvento.Value.Date + (_horaEvento ?? TimeSpan.Zero);
        else
            _evento.DataHora = null;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Delegates de validação MudBlazor ↔ FluentValidation
    // ─────────────────────────────────────────────────────────────────────────
    private Func<string?, IEnumerable<string>> _validarNome =>
        _ => _validator.ValidarCampo(_evento, nameof(EventoCreateDto.Nome));

    private Func<DateTime?, IEnumerable<string>> _validarDataHora =>
        _ => _validator.ValidarCampo(_evento, nameof(EventoCreateDto.DataHora));

    private Func<string?, IEnumerable<string>> _validarGenero =>
        _ => _validator.ValidarCampo(_evento, nameof(EventoCreateDto.GeneroMusical));

    private Func<string?, IEnumerable<string>> _validarLocal =>
        _ => _validator.ValidarCampo(_evento, nameof(EventoCreateDto.Local));

    private Func<string?, IEnumerable<string>> _validarDescricao =>
        _ => _validator.ValidarCampo(_evento, nameof(EventoCreateDto.Descricao));

    private Func<decimal?, IEnumerable<string>> _validarPreco =>
        _ => _validator.ValidarCampo(_evento, nameof(EventoCreateDto.Preco));

    private Func<int, IEnumerable<string>> _validarCapacidade =>
        _ => _validator.ValidarCampo(_evento, nameof(EventoCreateDto.CapacidadeMaxima));

    // ─────────────────────────────────────────────────────────────────────────
    // Ciclo de vida
    // ─────────────────────────────────────────────────────────────────────────
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;

        _dotNetRef = DotNetObjectReference.Create(this);
        await InicializarCryptoAsync();
        await InicializarDropZoneAsync();
    }

    private async Task InicializarCryptoAsync()
    {
        try
        {
            await CryptoSvc.InicializarAsync();
            _cryptoInicializado = true;
            _cryptoErro         = null;
        }
        catch (Exception ex)
        {
            _cryptoErro         = ex.Message;
            _cryptoInicializado = false;
            Snackbar.Add("Falha ao inicializar criptografia: " + ex.Message, Severity.Error);
        }
        finally
        {
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task InicializarDropZoneAsync()
    {
        if (!_cryptoInicializado) return;

        _dotNetRef = DotNetObjectReference.Create(this);
        try
        {
            await JS.InvokeVoidAsync("ticketPrimeCrypto.initDropZone", _dotNetRef, "ec-drop-zone");
        }
        catch (Exception ex)
        {
            // Drop zone é funcionalidade extra; falha não bloqueia o formulário
            Console.Error.WriteLine("[EventoCreate] Falha ao inicializar drop zone: " + ex.Message);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Callbacks invocadas pelo JavaScript (drag & drop)
    // ─────────────────────────────────────────────────────────────────────────

    [JSInvokable]
    public void OnDragEnter()
    {
        _isDragOver = true;
        InvokeAsync(StateHasChanged);
    }

    [JSInvokable]
    public void OnDragLeave()
    {
        _isDragOver = false;
        InvokeAsync(StateHasChanged);
    }

    [JSInvokable]
    public async Task OnFilesDropped(DroppedFileData[] files)
    {
        foreach (var file in files)
        {
            if (_fotos.Count >= MaxFotos) break;
            await AdicionarFotoAsync(file.Base64Data, file.Type, file.Name, file.Size);
        }
        await InvokeAsync(StateHasChanged);
    }

    [JSInvokable]
    public void OnDropErrors(string[] erros)
    {
        foreach (var erro in erros)
            Snackbar.Add(erro, Severity.Warning);
        InvokeAsync(StateHasChanged);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Upload via InputFile (clique)
    // ─────────────────────────────────────────────────────────────────────────

    private async Task HandleInputFileAsync(InputFileChangeEventArgs e)
    {
        var arquivos = e.GetMultipleFiles(MaxFotos - _fotos.Count);

        foreach (var arquivo in arquivos)
        {
            if (_fotos.Count >= MaxFotos)
            {
                Snackbar.Add($"Limite de {MaxFotos} fotos atingido.", Severity.Warning);
                break;
            }

            if (!TiposAceitos.Contains(arquivo.ContentType))
            {
                Snackbar.Add($"\"{arquivo.Name}\" – tipo não suportado (apenas JPG, PNG, WebP).", Severity.Warning);
                continue;
            }

            if (arquivo.Size > MaxTamanhoFoto)
            {
                Snackbar.Add($"\"{arquivo.Name}\" – excede 5 MB.", Severity.Warning);
                continue;
            }

            try
            {
                var base64 = await LerArquivoComoBase64Async(arquivo);
                await AdicionarFotoAsync(base64, arquivo.ContentType, arquivo.Name, arquivo.Size);
            }
            catch (Exception ex)
            {
                Snackbar.Add($"Erro ao ler \"{arquivo.Name}\": {ex.Message}", Severity.Error);
            }
        }
    }

    private static async Task<string> LerArquivoComoBase64Async(IBrowserFile arquivo)
    {
        await using var stream = arquivo.OpenReadStream(MaxTamanhoFoto);
        using var ms           = new MemoryStream();
        await stream.CopyToAsync(ms);
        return Convert.ToBase64String(ms.ToArray());
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Adicionar foto + criptografar
    // ─────────────────────────────────────────────────────────────────────────

    private async Task AdicionarFotoAsync(string base64, string mimeType, string nome, long tamanho)
    {
        // ── Regra: resolução mínima (800 × 600) ────────────────────────────────
        try
        {
            var dims = await JS.InvokeAsync<ImageDimensions>(
                "ticketPrimeCrypto.getImageDimensions", base64, mimeType);

            if (dims.Width < MinResolucaoLargura || dims.Height < MinResolucaoAltura)
            {
                Snackbar.Add(
                    $"\"{nome}\" – resolução insuficiente ({dims.Width}×{dims.Height}). " +
                    $"Mínimo exigido: {MinResolucaoLargura}×{MinResolucaoAltura} px.",
                    Severity.Warning);
                return;
            }
        }
        catch
        {
            // Se não conseguir ler dimensões, bloqueia o upload por segurança
            Snackbar.Add($"\"{nome}\" – não foi possível verificar as dimensões da imagem.", Severity.Error);
            return;
        }

        // ── Regra: sem fotos duplicadas (hash SHA-256 do conteúdo) ──────────────
        string hashConteudo;
        try
        {
            hashConteudo = await JS.InvokeAsync<string>("ticketPrimeCrypto.hashImageContent", base64);
        }
        catch
        {
            hashConteudo = nome + "|" + tamanho;   // fallback: nome+tamanho
        }

        if (!_hashesConteudo.Add(hashConteudo))
        {
            Snackbar.Add($"\"{nome}\" – imagem idêntica a uma já adicionada. Remova a duplicata.", Severity.Warning);
            return;
        }

        var foto = new FotoItem
        {
            ThumbnailDataUrl = $"data:{mimeType};base64,{base64}",
            MimeType         = mimeType,
            NomeArquivo      = nome,
            Tamanho          = tamanho,
            HashConteudo     = hashConteudo,
            Criptografando   = true
        };

        _fotos.Add(foto);
        _criptografandoFotos = true;
        await InvokeAsync(StateHasChanged);

        try
        {
            var dadosCifrados = await CryptoSvc.CriptografarImagemAsync(base64, mimeType, nome, tamanho);

            // Cria preview quebrado a partir dos primeiros bytes do ciphertext
            // (o navegador exibirá a imagem como corrompida, demonstrando que os dados são ilegíveis)
            var previewBase64 = dadosCifrados.CiphertextBase64.Length > 400
                ? dadosCifrados.CiphertextBase64[..400]
                : dadosCifrados.CiphertextBase64;

            foto.CiphertextPreviewDataUrl = $"data:{mimeType};base64,{previewBase64}";
            foto.DadosCriptografados      = dadosCifrados;
            foto.Criptografada            = true;
        }
        catch (Exception ex)
        {
            _fotos.Remove(foto);
            Snackbar.Add($"Erro ao criptografar \"{nome}\": {ex.Message}", Severity.Error);
        }
        finally
        {
            foto.Criptografando  = false;
            _criptografandoFotos = _fotos.Any(f => f.Criptografando);
            await InvokeAsync(StateHasChanged);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Remover foto
    // ─────────────────────────────────────────────────────────────────────────

    private void RemoverFoto(string id)
    {
        var foto = _fotos.FirstOrDefault(f => f.Id == id);
        if (foto is null) return;

        // Libera o hash para permitir re-adicionar a mesma imagem futuramente
        if (!string.IsNullOrEmpty(foto.HashConteudo))
            _hashesConteudo.Remove(foto.HashConteudo);

        _fotos.Remove(foto);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Abrir seletor de arquivos
    // ─────────────────────────────────────────────────────────────────────────

    private async Task AbrirSeletorArquivosAsync()
    {
        // Aciona o <input type="file"> oculto via JS
        await JS.InvokeVoidAsync("ticketPrimeCrypto.clickFileInput", "ec-file-input");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Criar evento – submit
    // ─────────────────────────────────────────────────────────────────────────

    private async Task CriarEventoAsync()
    {
        if (_form is null) return;

        await _form.Validate();

        if (!_formValido)
        {
            Snackbar.Add("Corrija os erros no formulário antes de continuar.", Severity.Warning);
            return;
        }

        if (_criptografandoFotos)
        {
            Snackbar.Add("Aguarde a criptografia das fotos terminar.", Severity.Info);
            return;
        }

        // ── Regra: mínimo de fotos obrigatórias ───────────────────────────────
        if (_fotos.Count < MinFotos)
        {
            Snackbar.Add($"Adicione pelo menos {MinFotos} foto da banda antes de criar o evento.", Severity.Warning);
            return;
        }

        var fotosNaoCifradas = _fotos.Where(f => !f.Criptografada).ToList();
        if (fotosNaoCifradas.Count > 0)
        {
            Snackbar.Add("Algumas fotos não foram criptografadas. Tente removê-las e adicioná-las novamente.", Severity.Warning);
            return;
        }

        _carregando = true;
        StateHasChanged();

        try
        {
            // ── Regra: limite de eventos ativos por organizador ─────────────────
            // Em produção: GET /api/eventos?organizador={cpf}&status=Publicado
            var eventosAtivos = await SimularBuscarEventosAtivosAsync();
            if (eventosAtivos >= MaxEventosAtivosOrg)
            {
                Snackbar.Add(
                    $"Você já possui {eventosAtivos} eventos ativos. " +
                    $"O limite é {MaxEventosAtivosOrg}. Encerre ou cancele um evento antes de criar outro.",
                    Severity.Error);
                return;
            }

            // ── Regra: conflito de local + horário ─────────────────────────────
            // Em produção: GET /api/eventos?local={local}&dataHora={dataHora}
            var temConflito = await SimularVerificarConflitoAsync(_evento.Local, _evento.DataHora);
            if (temConflito)
            {
                Snackbar.Add(
                    $"Já existe um evento no mesmo local e horário ({_evento.DataHora:dd/MM/yyyy HH:mm}). " +
                    "Escolha outra data, hora ou local.",
                    Severity.Error);
                return;
            }

            // ── Status inicial: Rascunho ────────────────────────────────────────
            _evento.Status = EventoStatus.Rascunho;

            var pacote = new PacoteImagem
            {
                Evento             = _evento,
                Fotos              = _fotos.Select(f => f.DadosCriptografados!).ToList(),
                ChavePublicaOrgJwk = CryptoSvc.OrganizadorChavePublica ?? string.Empty,
                Timestamp          = DateTime.UtcNow
            };

            // ── Simulação de chamada HTTP POST ──────────────────────────────
            // Em produção: var response = await Http.PostAsJsonAsync("/api/eventos", pacote);
            // O servidor recebe o pacote SEM descriptografar as imagens.
            // A descriptografia ocorreria separadamente com a chave privada do servidor.
            await Task.Delay(1800);   // Simula latência de rede

            // Simulação bem-sucedida
            Snackbar.Add("Evento criado com sucesso! As fotos foram enviadas de forma criptografada. 🎉",
                         Severity.Success, config => config.VisibleStateDuration = 6000);

            await Task.Delay(800);
            Navigation.NavigateTo("/");
        }
        catch (Exception ex)
        {
            Snackbar.Add("Erro ao criar evento: " + ex.Message, Severity.Error);
        }
        finally
        {
            _carregando = false;
            StateHasChanged();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Dispose
    // ─────────────────────────────────────────────────────────────────────────

    public async ValueTask DisposeAsync()
    {
        _dotNetRef?.Dispose();
        await CryptoSvc.DisposeAsync();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Simulações de chamadas de API (substituir por Http.GetFromJsonAsync em produção)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Simula GET /api/eventos?organizador={cpf}&amp;status=Publicado,Rascunho
    /// Em produção: retorna o número de eventos ativos do organizador logado.
    /// </summary>
    private static async Task<int> SimularBuscarEventosAtivosAsync()
    {
        await Task.Delay(300);   // latência simulada
        return 0;                // demo: organizador não tem eventos ativos
    }

    /// <summary>
    /// Simula GET /api/eventos/conflito?local={local}&amp;dataHora={dt}
    /// Em produção: retorna true se já houver outro evento no mesmo local ±1h do horário.
    /// </summary>
    private static async Task<bool> SimularVerificarConflitoAsync(string local, DateTime? dataHora)
    {
        await Task.Delay(300);
        return false;   // demo: sem conflito
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Tipos internos
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Estado local de uma foto na UI (pré e pós-criptografia).</summary>
    private sealed class FotoItem
    {
        public string             Id                       { get; set; } = Guid.NewGuid().ToString("N")[..8];
        public string             ThumbnailDataUrl         { get; set; } = string.Empty;
        public string             CiphertextPreviewDataUrl { get; set; } = string.Empty;
        public bool               Criptografando           { get; set; }
        public bool               Criptografada            { get; set; }
        public string             MimeType                 { get; set; } = string.Empty;
        public string             NomeArquivo              { get; set; } = string.Empty;
        public long               Tamanho                  { get; set; }
        /// <summary>SHA-256 do conteúdo bruto da imagem (deduplicação).</summary>
        public string             HashConteudo             { get; set; } = string.Empty;
        public FotoCriptografada? DadosCriptografados      { get; set; }
    }

    /// <summary>DTO para arquivos recebidos via drag &amp; drop do JavaScript.</summary>
    public sealed class DroppedFileData
    {
        public string Name       { get; set; } = string.Empty;
        public long   Size       { get; set; }
        public string Type       { get; set; } = string.Empty;
        public string Base64Data { get; set; } = string.Empty;
    }

    /// <summary>Dimensões de imagem retornadas pelo JS via ticketPrimeCrypto.getImageDimensions.</summary>
    private sealed class ImageDimensions
    {
        public int Width  { get; set; }
        public int Height { get; set; }
    }
}
