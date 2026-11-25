namespace Necro.Nucleo
{
    public enum TipoEvento
    {
        /// <summary>Alvo sofreu dano. Origem=atacante (ou -1), Alvo, Valor, TipoDano, Critico, Pos. Extra=valor absorvido por escudo.</summary>
        Dano,
        /// <summary>Alvo foi curado. Origem=curador (ou -1), Alvo, Valor, Pos.</summary>
        Cura,
        /// <summary>Uma unidade iniciou um golpe (para animação/projétil). Origem, Alvo, Pos=origem, Dir.</summary>
        Ataque,
        /// <summary>Entidade morreu. Alvo=entidade, Origem=matador, Condicao, Overkill, Execucao, Dir=direção do golpe, Pos, Especie, TipoDano. Extra=1 se foi Sacrifício (feitiço ou Manto do Último Suspiro), 2 se foi um minion temporário que expirou.</summary>
        Morte,
        /// <summary>Cadáver ficou na arena. Alvo=id do cadáver, Origem=id da entidade, Condicao, Pos, Especie.</summary>
        CadaverCriado,
        /// <summary>Cadáver consumido por feitiço. Alvo=id do cadáver, Pos, Extra=(int)FeiticoId.</summary>
        CadaverConsumido,
        /// <summary>Cadáver removido (limite da arena ou fim do andar). Alvo=id do cadáver, Pos.</summary>
        CadaverRemovido,
        /// <summary>Orbe de alma caiu. Alvo=id do orbe, Origem=entidade morta, Pos, Especie, Extra=(int)Qualidade.</summary>
        AlmaCaiu,
        /// <summary>Necromante começou a canalizar. Alvo=id do orbe, Pos.</summary>
        CeifaIniciada,
        /// <summary>Canalização interrompida por dano. Alvo=id do orbe, Valor=dano sofrido na canalização.</summary>
        CeifaInterrompida,
        /// <summary>Alma ceifada com sucesso. Alvo=id do orbe, Pos, Especie, Extra=(int)Qualidade.</summary>
        AlmaCeifada,
        /// <summary>Orbe sumiu sem ser ceifado. Alvo=id do orbe, Pos.</summary>
        OrbeExpirou,
        /// <summary>Necromante lançou feitiço. Origem=necromante, Alvo=entidade alvo (ou -1), Extra=(int)FeiticoId, Pos=ponto alvo, Dir, Valor=raio/comprimento.</summary>
        FeiticoLancado,
        /// <summary>Explosão/efeito em área (cadáver, Rancoroso, elite Explosivo, conjurador). Origem, Pos, Valor=raio, TipoDano.</summary>
        Explosao,
        /// <summary>Efeito de status aplicado. Alvo, Valor=duração. Extra: 1=Fraqueza, 2=Provocação, 3=Sacrifício (bônus de dano), 4=Fúria, 5=Último Suspiro (Alvo=necromante, Origem=minion sacrificado).</summary>
        EfeitoAplicado,
        /// <summary>Entidade entrou na arena (inimigo, ou minion temporário de lendário: Origem=necromante). Alvo=entidade, Pos, Especie. Extra=(int)ModificadorElite; Critico=é chefe.</summary>
        EntidadeSurgiu,
        /// <summary>Minion voltou via Reerguer. Alvo=entidade, Pos.</summary>
        MinionReerguido,
        /// <summary>Poça de sangue da Abadessa. Alvo=id da poça, Origem=chefe, Pos, Valor=raio.</summary>
        PocaCriada,
        PocaExpirou,
        /// <summary>Onda começou. Extra=número da onda, Valor=total de ondas, Critico=onda de elite/chefe.</summary>
        OndaIniciada,
        /// <summary>Andar começou. Extra=andar.</summary>
        AndarIniciado,
        /// <summary>Andar concluído. Extra=andar, Valor=segundos gastos.</summary>
        AndarConcluido,
        /// <summary>Ponto de Ancoragem alcançado. Extra=andar de onde o modo Avançar passa a partir.</summary>
        AncoragemAlcancada,
        /// <summary>Recurso ganho. Extra: 0=Ossos, 1=Sangue, 2=Restos (Condicao indica o tipo). Valor=quantidade, Pos.</summary>
        RecursoGanho,
        /// <summary>Fim da expedição. Extra=(int)MotivoFim, Valor=maior andar concluído.</summary>
        ExpedicaoEncerrada,
        /// <summary>Projétil disparado. Origem=quem disparou, Alvo=id do projétil, Pos, Dir, Especie. Extra=0 flecha, 1 Lança de Ossos (Valor=alcance).</summary>
        ProjetilLancado,
        /// <summary>Projétil chegou ao destino. Alvo=id do projétil, Origem=entidade atingida (ou -1 se errou), Pos.</summary>
        ProjetilTerminou,
        /// <summary>Um item caiu e já foi para o resultado da expedição (não há orbe para coletar). Origem=entidade morta, Alvo=índice em Resultado.Itens, Pos, Extra=(int)RaridadeItem, Valor=nível do item.</summary>
        ItemCaiu
    }

    /// <summary>
    /// Evento leve emitido pela simulação para a camada visual. A lista Simulacao.Eventos é limpa no início de cada Tick,
    /// então a camada visual deve consumi-la logo depois de chamar Tick().
    /// </summary>
    public struct EventoSim
    {
        public TipoEvento Tipo;
        public int Tick;
        public int Origem;
        public int Alvo;
        public float Valor;
        public TipoDano TipoDano;
        public bool Critico;
        public CondicaoCadaver Condicao;
        public bool Overkill;
        public bool Execucao;
        public Vec2 Pos;
        public Vec2 Dir;
        public EspecieId Especie;
        public int Extra;
    }
}
