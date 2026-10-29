# Documentos, evidências e arquivos protegidos

Implementação backend dos cards [80](https://trello.com/c/uCjvOHvX), [82](https://trello.com/c/2Z4HQWaL) e parte documental do [83](https://trello.com/c/w9vpbSC7), a partir da main b868f6d.

## Autorização e sigilo

A identidade vem da sessão (ICurrentUserService), nunca de AuthorId/ApproverId enviados. O servidor confere usuário, empresa, escopos EMPRESA/UNIDADE/SETOR, permissões persistidas, relações e estado ativo. Não existe exceção de acesso por ser ADMIN. Trabalhadores consultam documentos próprios; liderança com apenas permissões de liderança consulta documentos de membros das equipes sob sua chefia. Permissões amplas atribuídas por outro perfil são deliberadas e continuam limitadas pelo escopo.

Documentos e tipos Restrito/Medico ficam fora da busca geral e de suas contagens, inclusive quando houver concessão. A classificação efetiva considera o tipo, evitando publicação por rebaixamento do campo do documento. Não se altera classificação restrita para geral.

O acesso a registro restrito exige concessão explícita para documento/usuário, com expiração/revogação. CanReview é separado do acesso ao arquivo. Documento médico exige ainda as permissões aso:ler ou aso:editar, conforme a ação; isso não implementa decisão de aptidão clínica. A conferência exige também documento:aprovar e designação conferir ativa na mesma empresa/local. Administração não substitui essas condições.

Criar metadados restritos exige capacidade do conferente. Não concede leitura automática: antes do primeiro upload, uma concessão deve ser registrada expressamente pelo concedente autorizado.

## Cadastro e consulta

- POST /documents: contrato anterior preservado; personId opcional é um alias para links de EntityType TRABALHADOR. Não confundir ownerId (responsável) com titular da evidência. Vínculos são verificados na empresa/unidade/setor; referências de módulos futuros não são aceitas.
- GET /documents/{id}: retorna revision e currentVersionNumber, além dos campos existentes. Recursos fora do alcance retornam 404.
- GET /documents?page=1&pageSize=20: retorna items, page, pageSize e total.
- GET /documents/query: mesma resposta paginada, inclusive na página padrão.
- GET /documents sem o parâmetro page: preserva a lista legada. X-Total-Count é sempre calculado depois da autorização.
- Filtros documentais: companyId, unitId, departmentId, documentTypeId, personId, teamId, status, search, from, to, validUntilBefore. from/to são datas civis de emissão (YYYY-MM-DD), não instantes. pageNumber continua aceito como alias legado.
- GET /documents/query-policy: informa maxPageSize. Default 100, configurável em Documents:Search:MaxPageSize (1–200).
- Filtros repetidos, UUID vazio, página inválida, intervalo invertido ou página além do limite são recusados. Busca textual trata %, _ e \ como texto, não como curingas.

## Upload e download

- GET /documents/file-policy: informa tamanho máximo em bytes e tipos permitidos.
- POST /documents/{id}/files e /versions: multipart com file, expectedVersion (revisão atual do documento) e justification opcional. authorId legado não é usado como identidade. Retorna 201 com versão, storedFileId, hash, número e estado.
- GET /documents/{id}/files/{storedFileId}/download e /versions/{versionId}/download: stream protegido, Cache-Control: no-store, nosniff e download como anexo. Não há URL pública permanente nem rota de prévia que contorne a autorização.
- Configuração Documents:Files:MaxSizeBytes: default 10 MiB, máximo 50 MiB. AllowedMimeTypes: default PDF/PNG/JPEG; pode restringir a lista. Validação compara assinatura do conteúdo, MIME informado e extensão, além do tamanho real lido.
- Upload é validado antes de criar objeto ou referência. O armazenamento local escreve em .part e só publica o objeto após cópia concluída. Cancelamento/falha remove o temporário. Falha de persistência tenta remover o objeto sem referência.
- O storage usa chave aleatória e impede caminhos absolutos, travessia de diretórios e links internos do sistema de arquivos. O diretório deve ser exclusivo do processo/administrador autorizado.
- Download e aceite conferem existência, tamanho e SHA-256 do objeto ligado à versão.

## Conferência e concessões

Após upload, a versão fica EM_REVISAO, com isCurrent=false. O documento passa a apontar para o novo número pendente. Uma versão anterior aceita continua conservada; ela não satisfaz a evidência mais recente enquanto esta estiver pendente/reprovada.

POST /documents/{id}/versions/{versionId}/review aceita:

    {
      "expectedVersion": 2,
      "version": 1,
      "decision": "aceita",
      "reason": null
    }

expectedVersion é Document.revision, e version é o número da evidência. decision é aceita|reprovada; reprovação exige motivo de até 2000 caracteres. A rota também aceita número no segmento de versão. /approve e /reject de uma versão permanecem como aliases, exigindo expectedVersion; approverId legado é ignorado. O comando genérico de aprovação de documento sem versão é recusado para impedir aceite implícito da versão atual.

A decisão grava ator autenticado e data de revisão. Versão antiga, decisão repetida ou revisão concorrente retorna 409. Aceite e substituição da versão vigente são transacionais; a antiga deixa o índice vigente antes da publicação da nova, dentro da mesma transação. publish genérico só confirma uma evidência já aceita, sem promover arquivo pendente.

POST /documents/{id}/access-grants aceita:

    {
      "userId": "uuid",
      "canReview": false,
      "expiresAt": "2027-01-01T00:00:00Z",
      "expectedVersion": 2
    }

Concedente precisa de documento:aprovar, designação conferir vigente e, no caso médico, aso:editar. Destinatário mantém escopo e capacidade apropriados; concessão não fornece essas permissões. A revisão do documento é incrementada para que concessões concorrentes não sejam silenciosas.

POST /documents/{id}/access-grants/{grantId}/revoke?expectedVersion=1 exige a versão da concessão. Expiração/revogação produz recusa nas próximas leituras ou decisões. Não há duração real de concessão presumida pelo backend; a vigência é explícita.

## Migração e limites desta entrega

Document.Revision é token de concorrência; CurrentVersionNumber inicia em 0 para dados legados. Não se presume aceite válido de registros anteriores, cuja autoria antes podia ser enviada pelo cliente. É necessário novo upload e conferência explícita para constituir evidência pelo fluxo protegido. A política de migração de acervo real depende de revisão e validação da equipe.

A tabela docs.documento_concessao conserva concessões. Aplicar a migration da entrega antes de executar os novos endpoints.

Antivírus, backup agendado, retenção, storage externo e políticas operacionais de concessão não são homologados por esta implementação. Metadados de antivírus continuam PENDENTE; assinatura de arquivo e hash não substituem inspeção antimalware. Backups devem preservar o banco e os objetos do diretório configurado, com restauração conjunta testada. A instalação de agentes/infraestrutura de backup permanece dependente do ambiente real.

Convite anônimo limitado para anexos e concessão de aptidão médica não foram acrescentados. A geração de PDFs finais assinados e o ciclo final de PT ficam nos módulos próprios. Testes unitários não substituem a validação integrada PostgreSQL/API e operacional com dados fictícios. Nenhuma configuração de envio de e-mails foi alterada.
