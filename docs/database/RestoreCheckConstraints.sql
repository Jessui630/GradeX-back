BEGIN TRANSACTION;
ALTER TABLE [ResultadoCriterio] ADD CONSTRAINT [CK_ResultadoCriterio_AlgunValor] CHECK (([ValorTexto] IS NOT NULL OR [ValorNumerico] IS NOT NULL OR [ValorBooleano] IS NOT NULL OR [Observacion] IS NOT NULL));

ALTER TABLE [Notificacion] ADD CONSTRAINT [CK_Notificacion_FechaLectura] CHECK (([FechaLectura] IS NULL OR [FechaLectura]>=[FechaCreacion]));

ALTER TABLE [Evaluacion] ADD CONSTRAINT [CK_Evaluacion_ConditionGrade] CHECK (([ConditionGrade] IS NULL OR [ConditionGrade]>=(0) AND [ConditionGrade]<=(10)));

ALTER TABLE [Evaluacion] ADD CONSTRAINT [CK_Evaluacion_Dictamen] CHECK (([Dictamen] IS NULL OR ([Dictamen]=N'INCONCLUSO' OR [Dictamen]=N'NO_AUTENTICO' OR [Dictamen]=N'AUTENTICO')));

ALTER TABLE [Evaluacion] ADD CONSTRAINT [CK_Evaluacion_Fechas] CHECK (([FechaFin] IS NULL OR [FechaFin]>=[FechaInicio]));

ALTER TABLE [Evaluacion] ADD CONSTRAINT [CK_Evaluacion_TechnicalHealth] CHECK (([TechnicalHealth] IS NULL OR [TechnicalHealth]>=(0) AND [TechnicalHealth]<=(100)));

ALTER TABLE [Especialista] ADD CONSTRAINT [CK_Especialista_Estado] CHECK (([Estado]=N'SUSPENDIDO' OR [Estado]=N'INACTIVO' OR [Estado]=N'ACTIVO' OR [Estado]=N'PENDIENTE'));

ALTER TABLE [Especialista] ADD CONSTRAINT [CK_Especialista_Experiencia] CHECK (([AniosExperiencia] IS NULL OR [AniosExperiencia]>=(0)));

ALTER TABLE [EspecialidadEspecialista] ADD CONSTRAINT [CK_Especialidad_Nivel] CHECK (([Nivel]=N'EXPERTO' OR [Nivel]=N'AVANZADO' OR [Nivel]=N'INTERMEDIO' OR [Nivel]=N'GENERAL'));

ALTER TABLE [CriterioEvaluacion] ADD CONSTRAINT [CK_Criterio_TipoDato] CHECK (([TipoDato]=N'ESCALA' OR [TipoDato]=N'NUMERICO' OR [TipoDato]=N'TEXTO' OR [TipoDato]=N'BOOLEANO'));

ALTER TABLE [Certificado] ADD CONSTRAINT [CK_Certificado_Fechas] CHECK (([FechaVencimiento] IS NULL OR [FechaVencimiento]>=[FechaEmision]));

ALTER TABLE [AsignacionEspecialista] ADD CONSTRAINT [CK_Asignacion_Fechas] CHECK (([FechaFin] IS NULL OR [FechaFin]>=[FechaAsignacion]));

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261019010911_RestoreCheckConstraints', N'10.0.13');

COMMIT;
GO

