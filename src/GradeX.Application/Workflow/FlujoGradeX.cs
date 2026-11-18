namespace GradeX.Application.Workflow;

/// <summary>
/// Constantes técnicas del flujo GradeX. Los códigos ya existen en el esquema,
/// en los CHECK constraints o en la lógica previa; no son catálogo editable.
/// </summary>
public static class FlujoGradeX
{
    public static class EstadosSolicitud
    {
        public const string Borrador = "BORRADOR";
        public const string PendienteAsignacion = "PENDIENTE_ASIGNACION";
        public const string Asignada = "ASIGNADA";
        public const string EnEvaluacion = "EN_EVALUACION";
        public const string FaltaEvidencia = "FALTA_EVIDENCIA";
        public const string Finalizada = "FINALIZADA";
    }

    public static class Dictamenes
    {
        public const string Autentico = "AUTENTICO";
        public const string NoAutentico = "NO_AUTENTICO";
        public const string Inconcluso = "INCONCLUSO";

        public static readonly string[] Todos = [Autentico, NoAutentico, Inconcluso];
    }

    public static class TiposCriterio
    {
        public const string Escala = "ESCALA";
        public const string Numerico = "NUMERICO";
        public const string Texto = "TEXTO";
        public const string Booleano = "BOOLEANO";

        public static readonly string[] Todos = [Escala, Numerico, Texto, Booleano];
    }

    public static class EstadosEspecialista
    {
        public const string Pendiente = "PENDIENTE";
        public const string Activo = "ACTIVO";
        public const string Inactivo = "INACTIVO";
        public const string Suspendido = "SUSPENDIDO";

        public static readonly string[] Todos = [Pendiente, Activo, Inactivo, Suspendido];
    }

    public static class NivelesEspecialista
    {
        public const string General = "GENERAL";
        public const string Intermedio = "INTERMEDIO";
        public const string Avanzado = "AVANZADO";
        public const string Experto = "EXPERTO";

        public static readonly string[] Todos = [General, Intermedio, Avanzado, Experto];
    }

    public static class EstadosCertificado
    {
        public const string Emitido = "EMITIDO";
    }

    public static class TiposNotificacion
    {
        public const string CambioEstado = "CAMBIO_ESTADO";
        public const string SolicitudEvidencia = "SOLICITUD_EVIDENCIA";
        public const string EvidenciaRecibida = "EVIDENCIA_RECIBIDA";
        public const string CertificadoEmitido = "CERTIFICADO_EMITIDO";
    }

    public static class CamposProducto
    {
        public static readonly string[] Todos =
        [
            "NOMBRE", "MODELO", "SKU", "NUMERO_SERIE", "IMEI",
            "ACCESORIOS", "DESCRIPCION", "MARCA"
        ];
    }
}
