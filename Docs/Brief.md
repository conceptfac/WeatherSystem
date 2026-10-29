# Weather System — Brief de Arquitetura

Documento de referência do pacote de ambiente e clima. Simulação fisicamente embasada — sem APIs externas, sem dependência de serviços online.

---

## 1. Princípio central

**Tudo computado localmente.** Posição solar e lunar são calculadas a partir de fórmulas astronômicas com latitude, longitude, data/hora e UTC offset. Não há chamada de rede nenhuma, não há serviço de efemérides. O jogo funciona offline e em qualquer fuso horário.

---

## 2. Módulos

### 2.1 Sistema celestial (`Celestial/`)

Hierarquia de dependência:

```
WeatherSolarController  ←  autoridade da hora do dia / posição solar
        │
        └─► LunarController        (lê posição solar pra calcular fase lunar)
        └─► BuildingLights         (liga/desliga emissivos por limiar de hora)
        └─► LightPole              (poste de luz, liga no anoitecer)
```

- `SolarPositionCalculator` e `LunarPositionCalculator` são utilitários puros (sem MonoBehaviour) — podem ser usados diretamente em código de editor ou ferramentas sem precisar de cena.
- `SolarPositionData` e `LunarPositionData` são payloads imutáveis — o resultado de um cálculo não muda depois de criado.
- `LunarVisualPreset` — ScriptableObject, cor e intensidade da luz lunar por fase. Permite presets artísticos sem mexer no código.

**Convenção de coordenadas Unity**: Norte = `+Z`, Sul = `-Z`, Leste = `+X`, Oeste = `-X`. Toda saída de direção do sistema segue isso.

### 2.2 Sistema de nuvens (`Atmosphere/`)

- `NimbusCloudController` — controla o layer de nuvens em runtime. Tem `NimbusCloudRenderMode` (enum): `Flat`, `Volumetric`, `Hybrid`.
- `VolumetricCloudsVolume` — volume override do URP para a camada volumétrica. Precisa ser adicionado a um `Volume` na cena.
- `VolumetricCloudsURP` — Renderer Feature do URP. **Precisa ser adicionado ao asset de Renderer do URP em ProjectSettings**, não é suficiente ter o componente na cena.

### 2.3 Iluminação urbana

- `BuildingLights` — componente em qualquer objeto com janelas emissivas. Ativa/desativa materiais emissivos conforme o `WeatherSolarController` cruza o limiar dia/noite. Não precisa de Update próprio — reage a evento.
- `LightPole` — poste de luz de rua. Mesmo mecanismo do `BuildingLights`.

### 2.4 Tráfego

- `TrafficIntersectionController` — gerencia um cruzamento completo, controlando o ciclo de fases de vários `TrafficSignalUnit`.
- `TrafficSignalUnit` — uma cabeça de semáforo com estado vermelho/amarelo/verde. Não conhece outros semáforos — só recebe comandos do `TrafficIntersectionController`.

---

## 3. Dependência URP

O pacote requer Universal Render Pipeline. `VolumetricCloudsURP` é um Renderer Feature URP — sem URP, não compila. Não há suporte a HDRP ou Built-in RP planejado.

---

## 4. Pendente / próximos passos

- Precipitação (chuva, neve) — não implementado.
- Sistema de clima dinâmico (transição automática entre estados de tempo) — não implementado.
- Vento com efeito em vegetação — não implementado.
