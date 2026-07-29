# Camino a la Gloria — contexto principal

## Visión

Simulador single-player de carrera futbolística. La persona inicia a los 16 años, toma decisiones de alto impacto y construye un legado frente a una generación simulada de futbolistas.

## Prueba de carrera completa (27-07-2026)

- Se ejecutó una carrera completa desde creación hasta el retiro a los 40 años. Las entradas, decisiones, minijuegos, efectos y resumen se documentaron en `CAREER_RUN_REPORT.md`.
- Se corrigió la continuidad de calendario y tabla para clubes que ascienden, descienden o cambian de liga; el catálogo es una foto inicial, pero el club activo entra a su nueva división.
- Se incorporó retiro automático a los 40 años y el texto de resumen final en el estado de carrera.
- Deuda prioritaria: calibrar competencia, producción por posición, lesiones, descensos y diversidad de eventos. La prueba produjo demasiados campeonatos y cero asistencias, por lo que aún no refleja una distribución realista.
- El siguiente diseño debe conservar exactamente un minijuego manual por temporada, pero rotarlo por posición y contexto (penal, tiro libre, definición, último pase, intercepción o parada). Pretemporada y los eventos extracancha siguen siendo decisiones por clic.
- El palmarés actual solo registra campeonatos de liga por posición 1.º; faltan copa, continental, selección, premios individuales y récords con criterios de elegibilidad propios.

## Formatos de temporada que se implementarán

- La fase regular no puede entregar siempre el título. Cada liga declarará en el catálogo su fase final, ascenso/descenso y copas asociadas.
- Liga MX: torneo corto de 17 jornadas, Play-In para puestos 7.º–10.º, Liguilla desde cuartos y final; Apertura y Clausura se registrarán como torneos distintos.
- Championship: 46 jornadas, ascenso directo para 1.º–2.º y playoff de ascenso para puestos siguientes. La EFL anunció una ampliación del playoff desde 2026/27, por lo que el formato será versionado por temporada, no fijo.
- LALIGA EA SPORTS y Premier League: liga regular de ida/vuelta, descenso y copa nacional; LALIGA HYPERMOTION añade fase de ascenso después de sus 42 jornadas.
- J.League: el catálogo deberá versionar los cambios de formato de 2026 y los playoffs de transición, en vez de asumir una tabla genérica.
- Todas las copas y playoffs usarán rondas y resultados persistentes: sorteo, ida/vuelta o partido único, prórroga, penaltis, final y repercusión en títulos, dinero, fatiga, reputación y clasificación continental.

## Reglas innegociables

- Vue + TypeScript para la experiencia; ASP.NET Core 10 para todas las simulaciones.
- Sin base de datos: catálogos JSON versionados y partida persistida en IndexedDB del navegador.
- Catálogo real de referencia 2025/26–2026/27: Liga MX/Liga de Expansión MX, Premier League/EFL Championship, LALIGA EA SPORTS/LALIGA HYPERMOTION y J1/J2 League. No se incluyen escudos ni otros activos oficiales.
- Cada liga conserva cantidad de clubes, jornadas, formato, ascenso y descenso configurables.
- Carrera anual con tres a cinco eventos memorables; calendario, partidos, tabla y estadísticas se simulan completos.
- Árbol de decisiones contextual, determinista por semilla y sin repetición innecesaria.
- Todas las posiciones y reconversión con consecuencias.
- Interfaz limpia, de clics, estilo transmisión deportiva y móvil primero.
- Casino únicamente desde los 18 años, opcional, con dinero ficticio y propósito educativo; nunca dinero real.

## Estado actual

Implementado: API determinista, selección de nacionalidad/dorsal/liga/club, catálogo de clubes y competiciones, fixture persistente por liga, simulación jornada a jornada, tabla calculada con resultados, eventos anclados a partido/minuto/marcador/rival y transferencias en ventanas de mercado. El jugador tiene media por posición, atributos básicos, filigranas, pierna mala, moral, relaciones y riesgo de lesión. La interfaz muestra jornada, rival, marcador contextual y avance hasta el siguiente evento.

## Modelo obligatorio de eventos deportivos

- Ningún evento de carrera aparecerá aislado: deberá estar anclado a un partido, ventana de transferencias, corte de tabla, lesión, convocatoria o resultado de competición.
- El calendario genera todos los partidos de cada torneo. La interfaz solo presenta los encuentros y situaciones de mayor impacto, pero la tabla y estadísticas provienen del calendario completo.
- Cada partido conserva jornada, rival, localía, competición, marcador, minuto, importancia de tabla, rivalidad, condición física, rol, forma, alineación y objetivo del club.
- Los eventos de juego representan situaciones reales: ir perdiendo al minuto 70, proteger un resultado, penalti, expulsión, lesión, gol anulado, sustitución, clásico, final, descenso, ascenso, debut o convocatoria.
- Las decisiones son futbolísticas y contextualizadas: presionar o guardar energía, rematar o asociarse, arriesgar una entrada, jugar lesionado o pedir cambio, aceptar rotación, exigir minutos y negociar tras una actuación destacada.
- Cada resolución afecta variables persistentes: minutos, titularidad, forma, confianza, energía, lesión, relación con técnico/vestuario/hinchada, reputación, valor de mercado, interés de clubes, salario, estadísticas y tabla.
- Los eventos externos —prensa, agente, patrocinio, finanzas o polémica ficticia— requieren un detonante deportivo verificable: racha, clásico, lesión, traspaso, mala actuación o logro.
- El selector usa requisitos, pesos, exclusiones y enfriamiento; no repetirá la misma narrativa ni consecuencias incompatibles dentro de una temporada.
- Cada temporada programa aleatoriamente de tres a cinco eventos de partido ponderados por rival, jornada, tabla, forma, reputación y atributos. Solo el último y más decisivo activa un minijuego de precisión; los demás se resuelven con decisiones por clic.
- Cada temporada inicia con una pretemporada obligatoria: el motor propone tres atributos según posición, carencias, rendimiento, edad, lesiones y títulos; el usuario debe elegir dos. Cada mejora o caída usa una probabilidad ponderada por participación, promedio, goles, asistencias, edad, riesgo de lesión y palmarés.
- El resumen anual debe explicar la cadena causal: partido o decisión inicial, efecto inmediato, impacto en liga/copa y consecuencia para la siguiente temporada.

## Próximo trabajo

1. Añadir rivalidades, plantillas, estadios, colores y calendarios de ida/vuelta a las ligas catalogadas.
2. Implementar simulación de partidos, alineaciones, marcador/minutos y disparadores de eventos basados en contexto deportivo.
3. Persistir generación rival y ligar transferencias a rendimiento, reputación, agente, posición y necesidad de clubes.
4. Desarrollar los doce minijuegos y sus variantes por posición.
5. Añadir contratos detallados, prensa, lesiones, finanzas, premios y sistema de legado.
6. Añadir pruebas de motor, API y recorrido de navegador.

## Decisiones de interfaz y creación

- Antes de iniciar, el usuario elige nombre, nacionalidad, dorsal del 1 al 99, liga, club, posición, arquetipo y personalidad.
- La lista de clubes se filtra al elegir liga; ligas, clubes y competiciones proceden del catálogo JSON servido por la API.
- Las transferencias no son eventos aislados: una actuación en un partido importante activa scouts y ofertas; el usuario compara club, liga, salario y rol.

## Estado implementado: economia, contratos y archivo anual

- La moneda visible de carrera es EUR. Cada jugador tiene un saldo persistente, un contrato anual, salario bruto anual, deposito mensual neto, bono de firma y primas por aparicion, gol/asistencia y titulo.
- El libro mayor registra cada ingreso o egreso por temporada: nomina, primas, bonos de firma, inversiones ficticias, multas y movimientos financieros. El dinero es estrictamente interno a la partida; no existen pagos ni apuestas reales.
- Todo contrato vence al cierre de la temporada. Tras registrar el resultado anual se abre una negociacion obligatoria: renovar con el club, elegir una oferta o pedir salida al mercado. La nueva temporada inicia con contrato de un ano y pretemporada.
- La interfaz ya no requiere un boton de aceptar o avanzar tras resolver una decision. El motor sigue el calendario y encadena automaticamente el siguiente evento, el cierre anual o la negociacion contractual.
- Cada resolucion muestra el marcador final y si se gano, empato o perdio, junto con la causa narrativa y los efectos visibles de la decision sobre forma, energia, reputacion, relaciones o dinero.
- La cronologia global se complementa con un archivo expandible por temporada. Cerrado muestra solo PJ, goles, media y titulos; abierto muestra posicion final, eventos, resultados y movimientos del libro mayor.
- La partida se persiste en IndexedDB, con lectura de respaldo para partidas previas guardadas en localStorage.
- El catalogo operativo se completa a 90 eventos: los 60 eventos originales y 30 eventos de vida personal, directiva, finanzas e integridad. Los temas de casino y amaño son ficticios, solo se habilitan por edad cuando aplica y presentan consecuencias disciplinarias claras.

## Estado implementado: minijuegos funcionales

- El minijuego no acepta una puntuaciÃ³n libre. Cada desafÃ­o guarda estado, secuencia, dificultad e instrucciones; la API valida los clics y el resultado nunca se convierte despuÃ©s en un Ã©xito automÃ¡tico.
- Hay quince desafÃ­os: penal, tiro libre, definiciÃ³n, Ãºltimo pase, intercepciÃ³n, parada, duelo aÃ©reo, rondo, buscaminas, tres en raya, objetivos, foco, dados, ruleta y blackjack ficticios. Los siete primeros son de partido, cinco de habilidad/vida y tres de casino para mayores de edad.
- La interfaz presenta una arena vertical por reto: porterÃ­a de tres zonas y precisiÃ³n para tiros, tableros para memoria/minas/tres en raya, y mesa de casino con decisiÃ³n visible. No hay controles duplicados ni un clic de confirmaciÃ³n adicional.
- El selector usa un Ãºnico desafÃ­o anual: 70% cancha, 20% entrenamiento y 10% ocio para mayores de 18. No se puede repetir dentro de la misma temporada.
- En un partido decisivo, el fallo del desafio fija la accion fallida y su marcador coherente; no existe una tirada posterior que convierta un fallo en victoria.

## Estado implementado: experiencia final de carrera

- El panel principal comunica con color si forma, energÃ­a, moral, riesgo y atributos estÃ¡n bien, en alerta o mal. TambiÃ©n explica rol, minutos y pistas de progreso sin revelar umbrales internos.
- El archivo anual compacto muestra PJ, goles, media y tÃ­tulos; al abrirlo revela asistencias, minutos, selecciÃ³n, rol, valor, premios, hitos y el registro completo de sucesos de esa temporada.
- Los resultados de casino muestran el importe real en EUR de la partida y el libro mayor conserva el movimiento. El casino sigue siendo ficticio, opcional y sin dinero real.

## Registro de cambios

## Partido contextual y mercado por escalones

- Cada partido importante usa una situaciÃ³n concreta segÃºn posiciÃ³n, marcador y minuto: mano a mano, penal, centro, duelo aÃ©reo, salida de portero, presiÃ³n, tiro libre, contraataque, Ãºltimo pase o cierre defensivo. Las situaciones combinan los relatos del catÃ¡logo con grupos de acciones para evitar que el usuario vea siempre el mismo sÃ­ o no.
- Una situaciÃ³n presenta entre tres y cuatro acciones completas y se decide con un solo clic. Cada acciÃ³n declara los atributos FIFA que usa, su riesgo y los posibles impactos visibles de forma, fama y moral. La resoluciÃ³n usa media, atributo relevante, forma, energÃ­a, moral, rival, riesgo y presiÃ³n del partido; no existe un Ã©xito garantizado.
- Los desenlaces deportivos diferencian gol, asistencia, salvada, recuperaciÃ³n, despeje u ocasiÃ³n. Solo los goles y asistencias generan su prima contractual; los fallos de riesgo alto pueden conceder, mientras una opciÃ³n conservadora puede simplemente diluir la jugada.
- El perfil de mercado persistente resume puntuaciÃ³n, valor estimado, interÃ©s, clubes que observan y explicaciÃ³n. Su cÃ¡lculo pondera media, forma, reputaciÃ³n, moral, promedio de temporada, minutos/contribuciones, edad y riesgo de lesiÃ³n.
- Las ofertas se filtran antes de generarse: desarrollo hasta 52, primera/ascenso 60, primera fuerte 68, Champions 76 y Ã©lite global 82 de media de referencia. Una temporada excepcional de un jugador joven puede bajar como mÃ¡ximo tres puntos el umbral y abre un salto adelantado, pero nunca una oferta absurda directa.
- Cada club compara media mÃ­nima, fuerza de su plantilla, necesidad, presupuesto, reputaciÃ³n, forma, edad, lesiones y estrategia de reclutamiento. Una mala temporada baja compatibilidad, rol, salario e interÃ©s; una buena primero provoca seguimiento y luego ofertas de un escalÃ³n coherente.
- La interfaz muestra una tarjeta FIFA de PAC/SHO/PAS/DRI/DEF/PHY, fama, valor, interÃ©s y scouts, ademÃ¡s del uso de atributos e impactos de cada opciÃ³n de partido.

## Premios individuales

- Al cerrar cada temporada se evalúan premios por posición y rendimiento, no por una tirada aislada: Equipo de la temporada, Mejor jugador joven, Bota de Oro, Mejor asistidor, Defensa del año, Portero del año, Jugador de la temporada y Balón de Oro.
- Los requisitos combinan minutos, media, goles/asistencias, edad, posición, clasificación, títulos, media FIFA, prestigio de liga y —para el Balón de Oro— rendimiento internacional. Un delantero no puede obtener Bota de Oro sin producción real y un jugador de élite no recibe Balón de Oro sin una campaña extraordinaria.
- Cada galardón queda en el archivo de la temporada con su motivo, fama obtenida y premio económico. También aumenta moral, relación con medios/afición, valor estimado y oportunidades de mercado mediante reputación persistente.
- La calibración usa rangos históricos, no un rival individual simulado: la Bota de Oro exige 24 goles en élite global, 20 en liga fuerte, 16 en liga media y 13 en desarrollo; Mejor asistidor pide 14, 11, 9 o 7 asistencias respectivamente. Jugador de la temporada requiere media 7.45 y top 4; Balón de Oro exige media 84+, campaña 7.55+, título de club y presencia internacional. Estas reglas permiten campañas de mediocampistas, defensas y porteros sin forzarlos a producir cifras de delantero.

## Mundo simulado realista (corte 2025/26)

- La partida contiene un mundo persistente de clubes y mercados, sin base de datos ni consultas en vivo. El catálogo usa clubes y competiciones identificables como referencia; los futbolistas ajenos al usuario son perfiles ficticios generados y sus movimientos se guardan como mutaciones compactas en IndexedDB.
- Cobertura inicial: Liga MX/Liga de Expansión MX, Premier League/EFL Championship, LALIGA EA SPORTS/LALIGA HYPERMOTION, J1/J2, Primera División/Primera Nacional Argentina, Brasileirão Série A/Série B, Liga/Torneo BetPlay Colombia y MLS. MLS no usa ascenso ni descenso; las demás regiones conservan su jerarquía de dos divisiones.
- La foto base se fija al cierre 2025/26. Los formatos permanecen estables durante una carrera para evitar que una partida cambie sus reglas por una actualización externa; las competiciones de torneos cortos, playoffs, tabla regular y conferencias se distinguen mediante `FormatKey`.
- La fuerza inicial de un club se calcula con 45% palmarés histórico, 35% rendimiento de las últimas cinco campañas y 20% capacidad financiera/reclutamiento. La fuerza cambia después por resultados, ascensos, descensos y mercado: el historial importa, pero no garantiza títulos.
- Cada cierre de temporada simula todas las ligas, registra campeón, subcampeón, tabla, ascensos y descensos, y crea fichajes, cesiones o agentes libres para los clubes. Las ofertas al jugador comparan media, necesidad de puesto, fuerza de plantilla, presupuesto y rol prometido.
- Las escalas de salario, primas, presupuesto y fichaje dependen de cada liga. Premier, LaLiga, Brasil, MLS, Liga MX y J1 no comparten un multiplicador único. Las referencias externas se usan solo para calibración: Premier League, LALIGA, J.LEAGUE, AFA, DIMAYOR, CBF y Transfermarkt; no se descargan ni muestran datos en directo durante la partida.
- La interfaz incluye un bloque de Mundo simulado con campeones, movimientos, presupuesto y estrategia del club. En siguientes iteraciones se ampliará con pantalla de tabla por liga, copa y clasificación continental.

## Selecciones nacionales y eventos conectados

- Cada nacionalidad disponible recibe una selección con confederación, fuerza y palmarés de referencia. La carrera guarda internacionalidades, goles, convocatorias y campañas por temporada.
- Las convocatorias se calculan desde los 17 años con media, reputación, forma, resultado reciente, fuerza de la selección y competencia implícita. No toda promesa llega de inmediato: las selecciones potentes exigen un nivel más alto; también se contemplan explosiones tardías.
- Aceptar una convocatoria suma partidos, posible producción ofensiva, reputación, moral y relación con afición, pero consume energía. Rechazarla protege el calendario del club y genera coste ante medios/federación.
- Los eventos de Mundo se disparan por presupuesto, estrategia de reclutamiento y fichajes del club. La llegada de competencia o la exigencia de la directiva permite integrar el proyecto o exigir minutos, con consecuencia persistente sobre técnico, moral y reputación.
- La referencia de calibración incluye carreras internacionales tempranas, tardías y longevas, además de lesiones: Mbappé, Vardy, Modrić y De Bruyne. El juego no incorpora sus datos como contenido; los utiliza solo como criterio de diseño para frecuencia, duración y riesgo.

- 2026-07-28: se implementan contratos anuales en EUR, libro mayor, archivo expandible por temporada, flujo automatico de eventos, resultados explicitos e IndexedDB; se amplia el catalogo a 90 eventos.

- 2026-07-27: se crea el documento principal y el primer vertical funcional de carrera.
- 2026-07-27: se añaden nacionalidad, dorsal, liga y equipo elegibles; las transferencias se activan mediante actuaciones en partidos clave.
- 2026-07-27: se adopta un catálogo identificable de clubes y competiciones reales, con formatos nacionales configurables. La publicación comercial requerirá revisión de licencias de marcas.
- 2026-07-27: se establece que todos los eventos y decisiones deben tener detonante de partido o competición y consecuencias persistentes sobre la temporada.
- 2026-07-27: se implementa el primer motor de calendario, tabla real por resultados y eventos deportivos contextualizados.
- 2026-07-27: se añaden decisiones de entrenamiento, recuperación y prensa, siempre disparadas por un partido, con efectos persistentes sobre atributos, relaciones, energía, riesgo de lesión y media.
- 2026-07-27: se incorpora un único minijuego de precisión por temporada para el evento decisivo; su resultado modifica la probabilidad calculada por atributos y contexto.
- 2026-07-27: se implementa la pretemporada de dos elecciones entre tres atributos, con progresión y declive conscientes del rendimiento acumulado.
