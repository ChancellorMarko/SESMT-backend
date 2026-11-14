# Pendências de uso real: situação e decisões da equipe

Atualizado em 2026-11-11. Base: lista "O que ainda precisa ser desenvolvido/alinhado" de `PENDENCIAS_BACKEND_USO_REAL_2026-11-01.md` (frontend, `docs/handoff`).

Os PRs backend #41 a #48 e frontend #8 a #12 foram mesclados. Dois deles estavam empilhados e foram mesclados no branch de base, que já tinha entrado na `main`; por isso o conteúdo deles **não chegou à `main`** e foi reaberto em PRs de recuperação: backend #49 (conteúdo do #43) e frontend #13 (conteúdo do #12).

## Situação de cada item

| # | Pendência | Situação | PRs |
| --- | --- | --- | --- |
| 1 | Política comum de acesso | Na `main`: perfis `SESMT_PT_*` recebem `agenda:*`, `designacao:*` e `pt:*` conforme o perfil que a API exige em cada operação | backend #44 |
| 2 | Administração de usuários | Já existente: `GET /users/management-context` e `PUT /users/{id}/access`, consumidos pelo frontend nativo | — |
| 3 | Troca/desvinculação de conta | **Depende de decisão** (abaixo) | — |
| 4 | Cadastros restantes | Desativação de equipe com revisão, motivo, auditoria `TEAM_DEACTIVATED`, bloqueio por agendamentos pendentes e revogação de convites | frontend #13; backend (este PR) |
| 5 | Agenda | Na `main`: criação idempotente (`Idempotency-Key`) e catálogo de responsáveis elegíveis. Listagem de convites e pedido público **aguardam merge** | backend #42, #45; frontend #9, #10; backend #49 (aberto) |
| 6 | Revisão final da PT | Na `main`: aprovar/devolver, correção e reenvio com histórico | backend #46; frontend #11 |
| 7 | PDF final/snapshot | **Depende de decisão** (abaixo) | — |
| 8 | Comprovação da assinatura | **Depende de decisão** (abaixo) | — |
| 9 | Documentos específicos e outros módulos | **Depende de decisão** (abaixo) | — |
| 10 | Auditoria e jsonb | Na `main`: hash v3 sobre JSON canônico; eventos antigos reportados como não verificáveis, sem recálculo | backend #47 |

Também na `main`: backend #41 (permissões de EPI e treinamento perdidas nos merges) e frontend #8 (ajustes do roteiro de verificação real).

## PRs abertos e ordem de mescla

1. **backend #49**: listagem de convites e pedido público de agendamento. Vai primeiro: a `main` do frontend (#10) já chama esses endpoints.
2. **frontend #13**: desativação de equipe no modo oficial.
3. **frontend #17**: corrige o teste e2e de encerramento da PT, que falha na `main` e deixa todo PR do frontend com o CI vermelho.
4. **frontend #14, #15, #16 e #18**: otimizações de carregamento, tempo da suíte unitária e limpeza de imagens. Independentes entre si e dos anteriores.

Todos apontam para a `main`. Para não repetir a perda do #43 e do #12: um PR empilhado deve ser reapontado para a `main` **antes** de o PR de base ser mesclado.

## Decisões pedidas à equipe nos PRs

1. **#49 (antes #43), convite público:** quem tem o link vê o nome completo dos membros da equipe (como no modo demonstração). Alternativa: primeiro nome ou iniciais.
2. **#44, PT de GESTOR e TÉCNICO:** esses perfis continuam com `pt:*` sem ter perfil de PT. A interface mostra ações que a API recusa. Remover essas permissões é uma mudança de política.
3. **#46, revisor da PT:** hoje o revisor é definido só pela designação `revisar`. Exigir também um perfil (por exemplo, Configurador)?
4. **#46, aprovação definitiva:** uma PT aprovada não pode ser reaberta. Confirmar.

## Itens que dependem de definição de produto

### 3. Troca ou desvinculação da conta de um trabalhador

O vínculo inicial existe (`PUT /workers/{id}/account-binding`). Trocar ou remover a conta afeta ciências registradas, participação em PTs em curso e o histórico. Perguntas:
- Uma ciência dada pela conta anterior continua válida para a PT em curso?
- Desvincular durante uma PT em execução deve ser bloqueado?
- A troca exige motivo e quem pode fazê-la (RH, administrador)?

### 4. Impactos da desativação de equipe (decidido na implementação)

`DELETE /teams/{id}` deixou de desativar e agora responde 400 `TEAM_DEACTIVATION_CONFIRMATION_REQUIRED`. O fluxo novo:
- `GET /teams/{id}/deactivation-impact` (exige `equipe:excluir`): revisão atual, agendamentos que bloqueiam a desativação e quantidade de convites ativos.
- `POST /teams/{id}/deactivate` com `{ expectedVersion, reason }`: valida a revisão (409 se mudou) e exige motivo de 3 a 200 caracteres. Registra auditoria `TEAM_DEACTIVATED` e revoga os convites ativos da equipe.
- Bloqueio: um agendamento não cancelado com término futuro, ou com PT que não está concluída nem cancelada, impede a desativação (409 `TEAM_HAS_PENDING_APPOINTMENTS`). Nada é cancelado automaticamente, porque isso mexeria no planejamento de outras pessoas sem que elas soubessem.
- A resposta do bloqueio traz só identificador, datas e situações dos agendamentos, sem o título: quem desativa equipes não ganha leitura da agenda.

Ponto para a equipe confirmar: se preferirem cancelar ou transferir os agendamentos automaticamente, o bloqueio vira uma etapa a mais nesse fluxo.

### 7. PDF final e snapshot da PT

O frontend nativo mostra "A versão de impressão deste contrato ainda não está disponível". Perguntas:
- Em que momento o snapshot é gerado (liberação, conclusão, aprovação da revisão)?
- O que entra: contexto, versões dos modelos, respostas, participantes, ciências, documentos vinculados, revisão?
- O PDF é gerado pela API e guardado com hash, ou montado sob demanda a partir do snapshot?
- Quem pode baixá-lo?

### 8. Comprovação da assinatura

O bloqueio de documentos sem assinatura obrigatória existe; a comprovação, não. Perguntas:
- Qual mecanismo: aceite eletrônico com login e registro de hash da versão, certificado digital (ICP-Brasil) ou provedor externo?
- A assinatura expira ou pode ser revogada?
- Uma nova versão do conteúdo invalida a assinatura anterior automaticamente?

### 9. Documentos específicos e outros módulos

Perguntas:
- Quais tipos documentais precisam de formulário próprio a partir do `MetadataSchema` e quais campos são obrigatórios?
- Escopo aprovado para fluxos nativos de AT, PET e inspeções: quais entram na próxima entrega?
- Cadastro protegido de documentos médicos ou restritos: quais perfis leem e quais registram?
