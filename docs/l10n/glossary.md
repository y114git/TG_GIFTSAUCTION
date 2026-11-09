# G3MStudio terminology glossary

English is the source language. Target languages: ru, es, ja, ko, zh_cn, zh_tw. One term per concept per language. Use the chosen term everywhere, including tooltips, errors and docs.

## How to read this file

Markers after each term show where it comes from:

- (G3M): G3M's own language files (`G3M/src/assets/lang`). This is the house style and wins every tie.
- (VS): the VS Code language packs (`main.i18n.json` of vscode-loc, MIT). Only the main pack was read, so git extension strings are missing.
- (GM): GameMaker LTS 2026 IDE terms (`docs/research/gamemaker/gamemaker-terminology.md`). Its listed errors were not copied.
- (new): no usable source. The term is a proposal and needs a native review.

The terms used by shipped UI strings are also kept in `G3MStudio.Core/Glossary/glossary.json`; a test checks the language files against it. Add a term there when a string starts using a new concept.

Priority is G3M, then VS, then GM, then new. Where sources disagree, the "Conflicts resolved" section lists what was rejected and why.

GameMaker has no zh_tw pack, so zh_tw terms for GameMaker concepts are converted from zh_cn to Taiwan vocabulary and marked (new).

## Editor shell

| Concept (English) | ru | es | ja | ko | zh_cn | zh_tw |
| --- | --- | --- | --- | --- | --- | --- |
| workspace | Рабочая область (VS) | Área de trabajo (VS) | ワークスペース (VS, GM) | 작업 영역 (VS) | 工作区 (VS, GM) | 工作區 (VS) |
| panel | Панель (VS) | Panel (VS) | パネル (VS) | 패널 (VS) | 面板 (VS) | 面板 (VS) |
| tab | Вкладка (G3M) | Pestaña (G3M) | タブ (G3M) | 탭 (G3M) | 选项卡 (VS) | 頁籤 (VS) |
| command palette | Палитра команд (VS) | Paleta de comandos (VS) | コマンドパレット (VS) | 명령 팔레트 (VS) | 命令面板 (VS) | 命令選擇區 (VS) |
| quick open | Быстрое открытие (new) | Apertura rápida (new) | クイックオープン (new) | 빠른 열기 (new) | 快速打开 (VS) | 快速開啟 (VS) |
| settings | Настройки (G3M) | Configuración (G3M, VS) | 設定 (G3M, VS) | 설정 (G3M, VS) | 设置 (G3M, VS) | 設定 (G3M, VS) |
| keyboard shortcut | Сочетание клавиш (VS) | Método abreviado de teclado (VS) | キーボードショートカット (VS) | 바로 가기 키 (VS) | 键盘快捷方式 (VS) | 鍵盤快速鍵 (VS) |
| layout | Макет (VS) | Diseño (VS) | レイアウト (VS) | 레이아웃 (VS) | 布局 (VS) | 版面配置 (VS) |
| status bar | Строка состояния (VS) | Barra de estado (VS) | ステータスバー (VS) | 상태 표시줄 (VS) | 状态栏 (VS) | 狀態列 (VS) |
| output | Вывод (G3M, GM) | Salida (G3M, VS) | 出力 (G3M, VS) | 출력 (G3M, VS) | 输出 (G3M, VS) | 輸出 (G3M, VS) |
| problems | Проблемы (G3M, VS) | Problemas (G3M, VS) | 問題 (G3M, VS) | 문제 (G3M, VS) | 问题 (G3M, VS) | 問題 (G3M, VS) |
| notification | Уведомление (VS) | Notificación (VS) | 通知 (VS) | 알림 (VS) | 通知 (VS) | 通知 (VS) |
| search | Поиск (VS) | Buscar (G3M, VS) | 検索 (G3M, VS) | 검색 (G3M, VS) | 搜索 (G3M, VS) | 搜尋 (G3M, VS) |
| replace | Заменить (VS, GM) | Reemplazar (G3M, VS) | 置換 (G3M, VS) | 바꾸기 (VS) | 替换 (G3M, VS) | 取代 (G3M, VS) |
| find usages | Найти все ссылки (VS) | Buscar todas las referencias (VS) | すべての参照を検索 (VS) | 모든 참조 찾기 (VS) | 查找所有引用 (VS) | 尋找所有參考 (VS) |
| go to definition | Перейти к определению (VS) | Ir a la definición (VS) | 定義へ移動 (VS) | 정의로 이동 (VS) | 转到定义 (VS) | 移至定義 (VS) |
| rename | Переименовать (G3M, VS) | Renombrar (G3M) | 名前の変更 (G3M, VS) | 이름 변경 (G3M) | 重命名 (G3M, VS) | 重新命名 (G3M, VS) |
| undo | Отменить (VS, GM) | Deshacer (VS) | 元に戻す (VS) | 실행 취소 (VS) | 撤销 (G3M) | 復原 (G3M, VS) |
| redo | Повторить (VS) | Rehacer (VS) | やり直し (VS) | 다시 실행 (VS) | 重做 (VS, GM) | 重做 (VS) |
| save | Сохранить (G3M, VS) | Guardar (G3M, VS) | 保存 (G3M, VS) | 저장 (G3M, VS) | 保存 (G3M, VS) | 儲存 (G3M, VS) |
| save as | Сохранить как... (VS) | Guardar como... (VS) | 名前を付けて保存... (VS) | 다른 이름으로 저장... (VS) | 另存为... (VS) | 另存新檔... (VS) |
| autosave | Автосохранение (VS) | Autoguardado (VS) | 自動保存 (VS) | 자동 저장 (VS) | 自动保存 (VS) | 自動儲存 (VS) |
| backup | Резервная копия (G3M) | Copia de seguridad (G3M, VS) | バックアップ (G3M, VS) | 백업 (G3M, VS) | 备份 (G3M, VS) | 備份 (G3M, VS) |
| recovery | Восстановление (G3M) | Recuperación (G3M) | 復元 (G3M) | 복구 (G3M) | 恢复 (G3M) | 恢復 (G3M) |
| restore | Восстановить (G3M) | Restaurar (G3M) | 復元 (G3M) | 복원 (G3M) | 还原 (G3M) | 還原 (G3M) |
| local history | Локальная история (new) | Historial local (new) | ローカル履歴 (new) | 로컬 기록 (new) | 本地历史记录 (new) | 本機歷程記錄 (new) |
| open recent | Открыть последние (VS) | Abrir reciente (VS) | 最近使用した項目を開く (VS) | 최근 항목 열기 (VS) | 打开最近的文件 (VS) | 開啟最近使用的檔案 (VS) |
| start page | Стартовая страница (new) | Página de inicio (new) | スタートページ (new) | 시작 페이지 (new) | 起始页 (new) | 起始頁 (new) |

## Version control

| Concept (English) | ru | es | ja | ko | zh_cn | zh_tw |
| --- | --- | --- | --- | --- | --- | --- |
| source control | Система управления версиями (VS) | Control de código fuente (VS) | ソース管理 (VS, GM) | 소스 제어 (VS, GM) | 源代码管理 (VS) | 原始檔控制 (VS) |
| repository | Репозиторий (VS) | Repositorio (VS) | リポジトリ (VS) | 리포지토리 (VS) | 存储库 (VS) | 存放庫 (VS) |
| commit | Фиксация, глагол: Зафиксировать (VS) | Confirmar (VS) | コミット (VS, GM) | 커밋 (VS, GM) | 提交 (VS, GM) | 提交 (new) |
| branch | Ветвь (VS) | Rama (VS) | ブランチ (VS) | 분기 (VS) | 分支 (VS) | 分支 (VS) |
| stage | Индексировать (new) | Preparar (new) | ステージ (new) | 스테이징 (new) | 暂存 (new) | 暫存 (new) |
| unstage | Убрать из индекса (new) | Quitar de preparación (new) | ステージ解除 (new) | 스테이징 취소 (new) | 取消暂存 (new) | 取消暫存 (new) |
| discard | Отменить изменения (VS) | Descartar (VS) | 破棄 (VS) | 변경 내용 취소 (VS) | 放弃 (VS) | 捨棄 (VS) |
| pull | Получить (new) | Extraer (new) | プル (GM) | 끌어오기 (GM) | 拉取 (GM) | 提取 (new) |
| push | Отправить (new) | Enviar (new) | プッシュ (GM) | 푸시 (GM) | 推送 (GM) | 推送 (GM) |
| fetch | Извлечь (new) | Capturar (new) | フェッチ (VS) | 페치 (new) | 抓取 (new) | 擷取 (VS) |
| merge | Слияние, глагол: Объединить (G3M) | Fusión, verbo: Fusionar (G3M) | マージ (G3M) | 병합 (G3M) | 合并 (G3M) | 合併 (G3M) |
| conflict | Конфликт (G3M) | Conflicto (G3M) | 競合 (G3M) | 충돌 (G3M) | 冲突 (G3M) | 衝突 (G3M) |
| diff | Сравнение (G3M) | Diferencias (G3M) | 差分 (G3M) | 차이점 (G3M) | 差异 (G3M, VS) | 差異 (G3M) |
| compare (action) | Сравнить (G3M) | Comparar (G3M) | 比較する (G3M) | 비교 (G3M) | 对比 (G3M) | 比較 (G3M) |
| history | История (GM) | Historial (VS) | 履歴 (VS, GM) | 기록 (VS, GM) | 历史记录 (VS) | 歷程記錄 (VS) |
| blame | Авторство (new) | Autoría (new) | ブレーム (new) | 블레임 (new) | 追溯 (new) | 追溯 (new) |

## GameMaker resources and fields

| Concept (English) | ru | es | ja | ko | zh_cn | zh_tw |
| --- | --- | --- | --- | --- | --- | --- |
| data file | DATA-файл (G3M) | Archivo DATA (G3M) | DATAファイル (G3M) | DATA 파일 (G3M) | DATA 文件 (G3M) | DATA 檔案 (G3M) |
| resource | Ресурс (G3M, VS, GM) | Recurso (G3M, VS, GM) | リソース (G3M, VS, GM) | 리소스 (G3M, VS, GM) | 资源 (G3M, VS, GM) | 資源 (G3M, VS) |
| asset | Ассет (new) | Asset (new) | アセット (GM) | 에셋 (GM) | 素材 (new) | 素材 (new) |
| sprite | Спрайт (GM) | Sprite (GM) | スプライト (GM) | 스프라이트 (GM) | 精灵 (GM) | 精靈 (new) |
| frame | Кадр (GM) | Fotograma (new) | フレーム (GM) | 프레임 (GM) | 帧 (GM) | 影格 (new) |
| origin | Начало координат (new) | Origen (GM) | 原点 (GM) | 원점 (GM) | 原点 (GM) | 原點 (new) |
| collision mask | Маска столкновения (GM) | Máscara de colisión (GM) | コリジョンマスク (GM) | 충돌 마스크 (GM) | 碰撞遮罩 (GM) | 碰撞遮罩 (new) |
| texture page | Текстурная страница (new) | Página de texturas (new) | テクスチャページ (new) | 텍스처 페이지 (new) | 纹理页 (new) | 紋理頁 (new) |
| texture group | Группа текстур (GM) | Grupo de texturas (GM) | テクスチャグループ (GM) | 텍스처 그룹 (GM) | 纹理组 (GM) | 紋理組 (new) |
| background | Фон (G3M) | Fondo (G3M) | 背景 (G3M) | 배경 (G3M) | 背景 (G3M) | 背景 (G3M) |
| tile set | Набор плиток (GM) | Conjunto de tiles (new) | タイルセット (GM) | 타일 세트 (GM) | 图块集 (GM) | 圖塊集 (new) |
| tile | Плитка (GM) | Tile (new) | タイル (GM) | 타일 (GM) | 图块 (GM) | 圖塊 (new) |
| room | Комната (new) | Sala (GM) | ルーム (GM) | 룸 (GM) | 房间 (GM) | 房間 (new) |
| layer | Слой (GM) | Capa (GM) | レイヤー (GM) | 레이어 (GM) | 图层 (GM) | 圖層 (new) |
| instance | Экземпляр (GM) | Instancia (GM) | インスタンス (GM) | 인스턴스 (GM) | 实例 (GM) | 實例 (new) |
| view | Вид (new) | Vista (VS) | ビュー (VS) | 뷰 (GM) | 视图 (VS) | 視圖 (new) |
| viewport | Область просмотра (new) | Área de visualización (new) | ビューポート (new) | 뷰포트 (new) | 视口 (new) | 視口 (new) |
| camera | Камера (new) | Cámara (new) | カメラ (new) | 카메라 (new) | 摄像机 (new) | 攝影機 (new) |
| object | Объект (GM) | Objeto (GM) | オブジェクト (GM) | 오브젝트 (GM) | 物体 (GM) | 物件 (VS) |
| event | Событие (VS, GM) | Evento (VS, GM) | イベント (VS, GM) | 이벤트 (VS, GM) | 事件 (VS, GM) | 事件 (VS) |
| parent | Родитель (GM) | Padre (GM) | 親 (GM) | 상위 항목 (GM) | 父级 (new) | 父級 (new) |
| script | Скрипт (GM) | Script (GM) | スクリプト (GM) | 스크립트 (GM) | 脚本 (GM) | 指令碼 (new) |
| code | Код (G3M) | Código (G3M) | コード (G3M) | 코드 (G3M) | 代码 (G3M) | 程式碼 (G3M) |
| function | Функция (VS) | Función (VS) | 関数 (VS) | 함수 (VS) | 函数 (VS) | 函式 (VS) |
| variable | Переменная (VS, GM) | Variable (VS, GM) | 変数 (VS, GM) | 변수 (VS, GM) | 变量 (VS, GM) | 變數 (VS) |
| string | Строка (VS) | Cadena (VS) | 文字列 (VS) | 문자열 (VS) | 字符串 (VS) | 字串 (VS) |
| sound | Звук (G3M, GM) | Sonido (G3M, GM) | サウンド (G3M, GM) | 사운드 (G3M, GM) | 声音 (G3M, GM) | 音效 (G3M) |
| audio group | Аудиогруппа (GM) | Grupo de audio (GM) | オーディオグループ (GM) | 오디오 그룹 (GM) | 音频组 (GM) | 音訊組 (new) |
| font | Шрифт (G3M, VS, GM) | Fuente (G3M, VS, GM) | フォント (G3M, VS, GM) | 폰트 (G3M) | 字体 (G3M, VS, GM) | 字體 (G3M) |
| glyph | Глиф (VS) | Glifo (VS) | グリフ (VS) | 글리프 (new) | 字形 (VS) | 字形 (new) |
| path | Путь (G3M, GM) | Ruta (G3M, GM) | パス (G3M, GM) | 경로 (G3M, GM) | 路径 (G3M, GM) | 路徑 (G3M) |
| timeline | Временная шкала (VS, GM) | Línea de tiempo (VS) | タイムライン (VS, GM) | 타임라인 (VS, GM) | 时间轴 (GM) | 時間軸 (VS) |
| moment | Момент (new) | Momento (new) | モーメント (new) | 모멘트 (new) | 时刻 (new) | 時刻 (new) |
| shader | Шейдер (GM) | Shader (GM) | シェーダー (GM) | 셰이더 (GM) | 着色器 (GM) | 著色器 (new) |
| extension | Расширение (GM) | Extensión (VS, GM) | 拡張機能 (VS, GM) | 확장 기능 (GM) | 扩展 (VS, GM) | 延伸模組 (VS) |
| sequence | Последовательность (GM) | Secuencia (GM) | シーケンス (GM) | 시퀀스 (GM) | 序列 (GM) | 序列 (new) |
| track | Дорожка (new) | Pista (new) | トラック (new) | 트랙 (new) | 轨道 (new) | 軌道 (new) |
| keyframe | Ключевой кадр (new) | Fotograma clave (new) | キーフレーム (new) | 키프레임 (new) | 关键帧 (new) | 關鍵影格 (new) |
| animation curve | Кривая анимации (GM) | Curva de animación (GM) | アニメーションカーブ (GM) | 애니메이션 곡선 (GM) | 动画曲线 (GM) | 動畫曲線 (new) |
| particle system | Система частиц (new) | Sistema de partículas (new) | パーティクルシステム (new) | 파티클 시스템 (new) | 粒子系统 (GM) | 粒子系統 (new) |
| emitter | Эмиттер (new) | Emisor (VS) | エミッター (new) | 이미터 (new) | 发射器 (VS) | 發射器 (new) |
| general info | Общая информация (new) | Información general (new) | 一般情報 (new) | 일반 정보 (new) | 常规信息 (new) | 一般資訊 (new) |
| game options | Опции игры (GM) | Opciones del juego (GM, VS) | ゲームオプション (GM) | 게임 옵션 (GM) | 游戏选项 (GM) | 遊戲選項 (new) |
| bytecode | Байт-код (new) | Bytecode (new) | バイトコード (new) | 바이트코드 (new) | 字节码 (new) | 位元組碼 (new) |
| decompile | Декомпилировать (new) | Descompilar (new) | デコンパイル (new) | 디컴파일 (new) | 反编译 (new) | 反編譯 (new) |
| compile | Компилировать (GM) | Compilar (GM) | コンパイル (GM) | 컴파일 (GM) | 编译 (GM) | 編譯 (VS) |
| disassembly | Дизассемблирование (VS) | Desensamblado (VS) | 逆アセンブリ (VS) | 디스어셈블리 (VS) | 反汇编 (VS) | 反組譯碼 (VS) |
| asset browser | Обозреватель ассетов (new) | Explorador de assets (new) | アセットブラウザー (GM) | 에셋 브라우저 (GM) | 素材浏览器 (new) | 素材瀏覽器 (new) |
| inspector | Инспектор (GM) | Inspector (GM) | インスペクター (GM) | 검사기 (GM) | 检查器 (GM) | 檢查器 (new) |
| breakpoint | Точка останова (VS) | Punto de interrupción (VS) | ブレークポイント (VS, GM) | 중단점 (VS, GM) | 断点 (VS, GM) | 中斷點 (VS) |
| debug | Отладка (VS, GM) | Depurar (VS, GM) | デバッグ (VS, GM) | 디버그 (VS, GM) | 调试 (VS, GM) | 偵錯 (VS) |
| tag | Тег (VS) | Etiqueta (VS) | タグ (VS, GM) | 태그 (VS, GM) | 标签 (GM) | 標籤 (VS) |

## Modding and files

| Concept (English) | ru | es | ja | ko | zh_cn | zh_tw |
| --- | --- | --- | --- | --- | --- | --- |
| mod | Мод (G3M) | Mod (G3M) | MOD (G3M) | 모드 (G3M) | 模组 (G3M) | 模組 (G3M) |
| modpack | Модпак (G3M) | Modpack (G3M) | MODパック (G3M) | 모드팩 (G3M) | 模组包 (G3M) | 模組包 (G3M) |
| mod version | Версия мода (G3M) | Versión del mod (G3M) | MODバージョン (G3M) | 모드 버전 (G3M) | 模组版本 (G3M) | 模組版本 (G3M) |
| patch | Патч (G3M) | Parche (G3M) | パッチ (G3M) | 패치 (G3M) | 补丁 (G3M) | 補丁 (G3M) |
| patching (process) | Патчинг (G3M) | Parcheo (G3M) | パッチ処理 (G3M) | 패치 (G3M) | 补丁 (G3M) | 修補 (G3M) |
| apply | Применить (G3M, VS, GM) | Aplicar (G3M, VS, GM) | 適用 (G3M, VS, GM) | 적용 (G3M, VS, GM) | 应用 (G3M, VS, GM) | 套用 (G3M, VS) |
| create patch | Создать патч (G3M) | Crear parche (G3M) | パッチを作成 (G3M) | 패치 생성 (G3M) | 创建补丁 (G3M) | 建立補丁 (G3M, VS) |
| merge patches | Объединить патчи (G3M) | Fusionar parches (G3M) | パッチをマージ (G3M) | 패치 병합 (G3M) | 合并补丁 (G3M) | 合併補丁 (G3M) |
| validate | Проверить (new) | Validar (new) | 検証 (new) | 유효성 검사 (new) | 验证 (new) | 驗證 (new) |
| export | Экспорт (G3M, VS, GM) | Exportar (G3M, VS, GM) | エクスポート (G3M, VS, GM) | 내보내기 (G3M, VS, GM) | 导出 (G3M, VS, GM) | 匯出 (G3M, VS) |
| import | Импорт (G3M, VS, GM) | Importar (G3M, VS, GM) | インポート (G3M, VS, GM) | 가져오기 (G3M, VS, GM) | 导入 (G3M, VS, GM) | 匯入 (G3M, VS) |
| package | Пакет (G3M, VS) | Paquete (G3M, VS) | パッケージ (G3M, VS) | 패키지 (G3M, VS) | 包 (G3M, VS), only inside compounds such as 模组包, 支持包 | 套件 (VS) |
| game | Игра (G3M) | Juego (G3M) | ゲーム (G3M) | 게임 (G3M) | 游戏 (G3M) | 遊戲 (G3M) |
| chapter | Глава (G3M) | Capítulo (G3M) | チャプター (G3M) | 챕터 (G3M) | 章 (G3M) | 章 (G3M) |
| launch game | Запустить игру (G3M) | Iniciar el juego (G3M) | ゲームを起動 (G3M) | 게임 실행 (G3M) | 启动游戏 (G3M) | 啟動遊戲 (G3M) |
| run (a tool) | Выполнить (G3M) | Ejecutar (G3M, VS) | 実行 (VS, GM) | 실행 (G3M, VS) | 执行 (G3M) | 執行 (G3M, VS) |
| log | Лог (G3M) | Registro (G3M, VS) | ログ (G3M, VS) | 로그 (G3M, VS) | 日志 (G3M, VS) | 日誌 (G3M) |
| diagnostics | Диагностика (G3M) | Diagnóstico (G3M) | 診断 (G3M) | 진단 (G3M) | 诊断 (G3M) | 診斷 (G3M) |
| profile | Профиль (G3M) | Perfil (G3M) | プロファイル (G3M) | 프로필 (G3M) | 配置 (G3M) | 設定檔 (G3M) |
| plugin | Плагин (G3M) | Plugin (G3M) | プラグイン (G3M) | 플러그인 (G3M) | 插件 (G3M) | 外掛 (G3M) |
| desktop shortcut | Ярлык (G3M) | Acceso directo (G3M) | ショートカット (G3M) | 바로가기 (G3M) | 快捷方式 (G3M) | 捷徑 (G3M) |
| file | Файл (G3M, VS) | Archivo (G3M, VS) | ファイル (G3M, VS) | 파일 (G3M, VS) | 文件 (G3M, VS) | 檔案 (G3M, VS) |
| folder | Папка (G3M, VS) | Carpeta (G3M, VS) | フォルダー (G3M, VS) | 폴더 (G3M, VS) | 文件夹 (G3M, VS) | 資料夾 (G3M, VS) |
| original file | Оригинал (G3M) | Archivo original (G3M) | 元ファイル (G3M) | 원본 (G3M) | 原始文件 (G3M) | 原始檔案 (G3M) |
| modified file | Модифицированный файл (G3M) | Archivo modificado (G3M) | 変更後ファイル (G3M) | 수정본 (G3M) | 修改文件 (G3M) | 修改檔案 (G3M) |
| report | Отчёт (G3M) | Informe (G3M) | レポート (G3M) | 보고서 (G3M) | 报告 (G3M) | 報告 (G3M) |
| open | Открыть (VS) | Abrir (VS) | 開く (VS) | 열기 (VS) | 打开 (VS) | 開啟 (VS) |
| select | Выбрать (G3M) | Seleccionar (G3M) | 選択 (G3M) | 선택 (G3M) | 选择 (G3M) | 選擇 (G3M) |
| delete | Удалить (G3M, VS) | Eliminar (G3M, VS) | 削除 (G3M, VS) | 삭제 (G3M, VS) | 删除 (G3M, VS) | 刪除 (G3M, VS) |
| remove | Удалить (G3M) | Eliminar (G3M) | 削除 (G3M) | 제거 (G3M) | 移除 (G3M) | 移除 (G3M) |
| cancel | Отмена (G3M, VS) | Cancelar (G3M, VS) | キャンセル (G3M, VS) | 취소 (G3M, VS) | 取消 (G3M, VS) | 取消 (G3M, VS) |
| close | Закрыть (G3M, VS) | Cerrar (G3M, VS) | 閉じる (G3M, VS) | 닫기 (G3M, VS) | 关闭 (G3M, VS) | 關閉 (G3M, VS) |
| enable / disable | Включить / Отключить (VS) | Habilitar / Deshabilitar (VS) | 有効にする / 無効にする (VS) | 활성화 / 비활성화 (G3M, VS) | 启用 / 禁用 (VS) | 啟用 / 停用 (VS) |
| install | Установить (VS) | Instalar (VS) | インストール (VS) | 설치 (VS) | 安装 (VS) | 安裝 (VS) |
| download | Скачать (VS) | Descargar (VS) | ダウンロード (VS) | 다운로드 (VS) | 下载 (VS) | 下載 (VS) |
| version | Версия (G3M, VS) | Versión (G3M, VS) | バージョン (G3M, VS) | 버전 (G3M, VS) | 版本 (G3M, VS) | 版本 (G3M, VS) |
| name | Имя (G3M, VS) | Nombre (G3M, VS) | 名前 (G3M, VS) | 이름 (G3M, VS) | 名称 (G3M, VS) | 名稱 (G3M, VS) |
| warning | Предупреждение (G3M, VS) | Advertencia (G3M, VS) | 警告 (G3M, VS) | 경고 (G3M, VS) | 警告 (G3M, VS) | 警告 (G3M, VS) |
| error | Ошибка (G3M, VS) | Error (G3M, VS) | エラー (G3M, VS) | 오류 (G3M, VS) | 错误 (G3M, VS) | 錯誤 (G3M, VS) |

## Application

| Concept (English) | ru | es | ja | ko | zh_cn | zh_tw |
| --- | --- | --- | --- | --- | --- | --- |
| update | Обновить, сущ.: Обновление (G3M, VS) | Actualizar, sust.: Actualización (G3M, VS) | 更新 (G3M, VS) | 업데이트 (G3M, VS) | 更新 (G3M, VS) | 更新 (G3M, VS) |
| check for updates | Проверить обновления (G3M, VS) | Buscar actualizaciones (VS) | 更新を確認 (G3M, VS) | 업데이트 확인 (G3M, VS) | 检查更新 (G3M, VS) | 檢查更新 (G3M, VS) |
| release notes | Список изменений (G3M) | Registro de cambios (G3M) | 変更履歴 (G3M) | 변경 내역 (G3M) | 更新日志 (G3M) | 更新日誌 (G3M) |
| language | Язык (G3M, VS) | Idioma (G3M, VS) | 言語 (G3M, VS) | 언어 (G3M, VS) | 语言 (G3M, VS) | 語言 (G3M, VS) |
| theme | Тема (G3M, VS) | Tema (G3M, VS) | テーマ (G3M, VS) | 테마 (G3M, VS) | 主题 (G3M, VS) | 主題 (G3M) |
| file association | Сопоставление файлов (new) | Asociación de archivos (new) | ファイルの関連付け (new) | 파일 연결 (new) | 文件关联 (new) | 檔案關聯 (new) |
| onboarding (Getting started) | Начало работы (G3M) | Primeros pasos (G3M) | はじめに (G3M) | 시작하기 (G3M) | 入门指南 (G3M) | 入門指南 (G3M) |
| onboarding tour | Обучение (G3M) | Guía (G3M) | ガイド (G3M) | 안내 (G3M) | 指南 (G3M) | 指南 (G3M) |

## Conflicts resolved

Each row is a concept where two or more sources gave different terms. "Chosen" follows the priority G3M, VS, GM unless the reason says otherwise.

| Concept | Lang | Chosen | Rejected | Reason |
| --- | --- | --- | --- | --- |
| tab | zh_cn | 选项卡 (VS) | 标签 (G3M) | 标签 is also tag and label in GM; the G3M string "隐藏库标签" should move to 选项卡 |
| tab | zh_tw | 頁籤 (VS) | 標籤 (G3M) | 標籤 is also tag and label; same string to update |
| command palette | ja | コマンドパレット | コマンド パレット (VS) | G3M writes katakana compounds without spaces |
| settings | ru | Настройки (G3M) | Параметры (VS) | G3M uses Настройки 23 times against 3; GM agrees |
| settings | es | Configuración (G3M, VS) | Ajustes (G3M, GM) | Configuración is used 27 times against 12 and matches VS |
| settings | zh_tw | 設定 | 設置 (G3M, 5 uses) | 設定 is the Taiwan term and the G3M majority |
| output | ru | Вывод (G3M, GM) | Выходные данные (VS) | G3M already uses "Папка вывода"; shorter |
| replace | ko | 바꾸기 (VS) | 교체 (G3M), 대체 (GM) | Pairs with 찾기; keep 교체 only for the file-replace action in manual install |
| rename | es | Renombrar (G3M, GM) | Cambiar nombre (VS) | House style |
| rename | ko | 이름 변경 (G3M) | 이름 바꾸기 (VS) | House style; avoids a clash with 바꾸기 for replace |
| undo | ko | 실행 취소 (VS) | 취소 (GM) | GM term equals Cancel |
| undo | zh_cn | 撤销 (G3M) | 撤消 (VS, GM) | G3M uses 撤销 in confirmations |
| undo | ru | Отменить (VS, GM) | none | GM also uses Отменить for Cancel; Cancel buttons use the noun Отмена, Отменить is only for undo and discard |
| redo | ru | Повторить (VS) | Вернуть (VS), Переделать (GM) | Pairs with Отменить; Переделать is colloquial |
| redo | zh_cn | 重做 (VS, GM) | 恢复 (VS) | 恢复 means recover in G3M |
| backup | ru | Резервная копия | бекап (G3M status text) | Slang; G3M mod-update text already says "резервную версию" |
| history | ru | История (GM) | Журнал (VS) | Журнал is used for logs in VS and for the changelog in G3M errors |
| commit | zh_tw | 提交 | 認可 (VS terminal) | 認可 means approval; 提交 is the common developer term |
| commit | es | Confirmar (VS) | Enviar cambios (GM) | GM wording collides with push (Enviar) |
| commit | ru | Фиксация, Зафиксировать (VS) | Подтвердить изменения (GM) | Подтвердить means confirm |
| discard | ru | Отменить изменения (VS) | Удалить (VS, other uses) | Удалить means delete |
| merge | es | Fusión (G3M) | Combinación (VS) | House style |
| conflict | ja | 競合 (G3M) | 紛争 (G3M, once), コンフリクト (GM) | 紛争 means dispute |
| diff | zh_cn | 差异 (G3M report, VS) | 对比 (G3M tab) | 对比 kept only for the compare action |
| diff | zh_tw | 差異 (G3M report) | 比較 (G3M tab) | 比較 kept only for the compare action |
| diff | es | Diferencias (G3M report) | Comparar (G3M tab) | Comparar kept only for the compare action |
| asset | ru | Ассет (new) | Актив (GM) | Актив means financial asset; Ресурс is taken by resource |
| asset | es | Asset (new) | Activo (GM) | Activo means financial asset; Recurso is taken by resource |
| asset | zh_cn, zh_tw | 素材 (new) | 资源 (GM) | 资源 is already resource |
| asset browser | ru | Обозреватель ассетов (new) | Браузер актива (GM) | Listed GM error: wrong noun and grammar |
| frame | es | Fotograma (new) | Frame (GM) | Standard Spanish animation term; consistent with keyframe |
| origin | ru | Начало координат (new) | Источник (GM) | Источник means source |
| room | ru | Комната (new) | Рум (GM) | Transliteration, unclear to players |
| tile set | es | Conjunto de tiles (new) | Conjunto de casillas (GM) | Casilla means box or board square |
| instance | es | Instancia (GM) | Estancia (GM variant) | Estancia means stay or ranch |
| texture group | ru | Группа текстур (GM) | Группы текстуры (GM) | Singular needs the genitive plural |
| view (room) | ko | 뷰 (GM) | 보기 (VS) | 보기 is the generic View menu |
| object | zh_cn | 物体 (GM) | 对象 (VS) | Separates the GameMaker object from programming objects; use 对象 in code contexts |
| timeline | zh_cn | 时间轴 (GM) | 时间线 (VS) | Matches zh_tw 時間軸 |
| timeline | es | Línea de tiempo (VS) | Línea temporal (GM) | VS wording is the common one |
| font | ko | 폰트 (G3M) | 글꼴 (VS, GM) | House style |
| font | zh_tw | 字體 (G3M) | 字型 (VS) | House style |
| glyph | ko | 글리프 (new) | 문자 모양 (VS) | VS renders a phrase, not a term |
| glyph | zh_tw | 字形 (new) | 字元 (VS) | 字元 means character |
| extension | ko | 확장 기능 (GM) | 확장 (VS) | Keeps GameMaker extensions distinct from editor plugins |
| extension | zh_tw | 延伸模組 (VS) | 擴充功能 | Only sourced term; separate from 外掛 (plugin) |
| game options | ru | Опции игры (GM) | Параметры (VS) | Параметры clashes with Настройки; the GM label matches the IDE users see |
| game options | es | Opciones del juego | Opciones de juego (GM) | Article form reads better |
| breakpoint | ru | Точка останова (VS) | Точки разрыва (GM) | Listed GM error |
| patch | zh_tw | 補丁 (G3M, 48 uses) | 修補程式 (G3M, once), 修補檔 (G3M, once) | Majority; 修補 stays for the patching process |
| create | ko | 생성 (G3M) | 만들기 (G3M mod and modpack strings) | 생성 is used for patch, profile and shortcut; change the 만들기 strings on the next pass |
| create | zh_tw | 建立 (G3M, VS) | 創建 (G3M) | 創建 is mainland usage |
| import | ko | 가져오기 (VS) | 불러오기 (G3M) | G3M splits 16 against 14; VS and the pairing with 내보내기 break the tie |
| import | zh_tw | 匯入 (G3M, VS) | 導入 (G3M) | 導入 is mainland usage |
| chapter | ja | チャプター (G3M tabs) | 章 (G3M title) | Matches the game's own chapter names |
| log | ru | Лог (G3M) | Журнал (VS) | G3M uses Лог 26 times against 3 |
| log | zh_tw | 日誌 (G3M) | 記錄 (VS) | House style |
| check for updates | es | Buscar actualizaciones (VS) | Comprobar, Verificar (G3M, once each) | G3M is split; VS breaks the tie |
| check for updates | ja | 更新を確認 | アップデートのチェック (G3M, once) | Matches 更新 used elsewhere |
| release notes | all | G3M changelog term | VS terms (Заметки о выпуске, Notas de la versión, リリースノート, 릴리스 정보, 发行说明, 版本資訊) | G3M ships a Changelog button; release notes is the same page |
| theme | zh_tw | 主題 (G3M) | 佈景主題 (VS) | Shorter, house style |
| mod | ja | MOD (G3M, 197 uses) | Mod (G3M, 93 uses), モジュール (G3M, once) | Majority; モジュール is wrong |
| mod | es | Mod (G3M) | Modificación (G3M, once) | House style |
| run | ja | 実行 (VS, GM) | 走る (G3M, once) | 走る means to run on foot |
| profile | ja | プロファイル (G3M, 32 uses) | プロフィール (G3M, 8 uses) | Majority; プロフィール means a personal profile |
| profile | zh_cn | 配置 (G3M) | 配置文件 (VS) | House style, 80 uses |
| fetch | ko | 페치 (new) | 가져오기 (VS) | 가져오기 is import |
| pull | ko | 끌어오기 (GM) | 풀 | GM term is clear and distinct from 푸시 |
| start page | all | Own term (new) | Welcome page (VS) | Welcome page and onboarding tour are separate screens in G3MStudio |

## Concepts without a source

These have no G3M, VS or GM term in at least the listed languages. Every such cell is (new) and needs a native review before release.

- The git actions stage, unstage, pull, push, fetch and blame: the VS Code git extension pack was not downloaded, only the main pack. GM supplies pull and push for ja, ko and zh. Check the rest against the git extension strings in vscode-loc.
- Local history, moment, texture page, bytecode, decompile, validate, viewport, camera, track, keyframe, general info, file association, start page: all six languages.
- Particle system: only zh_cn has a source (GM).
- zh_tw for most GameMaker resource types: converted from zh_cn.

## Style rules per language

### All languages

- Placeholders (`{count}`, `{name}`) stay as written and keep the English names.
- Ellipsis for commands that open a dialog is three ASCII dots (`...`), as in G3M.
- Do not translate product and file names: G3M, G3MStudio, GameMaker, UNDERTALE, DELTARUNE, `data.win`, `g3mpatch`, GameBanana, xdelta.
- Terms in the tables are the base form. Inflect for case and number as the language needs, but keep the stem.
- Do not use "(s)" or "(es)" suffixes in new strings.
- Do not store VS Code style `&&` menu markers in strings.

### ru

- Address: formal plural. G3M writes instructions in the polite imperative ("Выберите", "Проверьте"). Buttons and menu items use the infinitive ("Сохранить", "Открыть"). VS Code uses the same split. Do not use the informal form.
- Capitalization: sentence case in menus, buttons, tabs and titles ("Показать все команды"). G3M still has Title Case in a few labels ("Главное Меню"); fix on the next pass.
- Quotes: «ёлочки» for names and quoted UI labels. G3M has 21 of these against 12 straight quotes; use «» only. Keep the letter ё where it is correct (G3M writes "Отчёт").
- Plurals: three forms plus a fractional form (1 файл, 2 файла, 5 файлов). Use the plural system of the string table, not "(ов)". Where no plural system exists, use the pattern "Label: {count}" ("Доступно обновлений: {count}"), which G3M already uses. Fix "{count} мода" in `profiles.game_mods`, which is wrong for counts of 5 and above.
- Loanwords such as "Лог", "Патч", "Модпак" and "Ассет" decline like native nouns.

### es

- Address: informal "tú" in prose. G3M uses it 89 times ("Elige", "Comprueba", "Selecciona") against 11 formal forms ("Seleccione", "¿Está seguro de que desea"). VS Code uses the formal "usted" in messages ("Pruebe", "Le damos la bienvenida"). Follow G3M and convert the remaining formal strings on the next pass ("¿Seguro que quieres...?").
- Buttons, menus and commands use the infinitive ("Guardar", "Abrir", "Aplicar").
- Capitalization: sentence case ("Cambiar fuente", "Guardar tema"). G3M still has Title Case labels ("Cambiar Fondo", "Guardar Tema"); fix on the next pass.
- Punctuation: the opening marks `¿` and `¡` are required. Use straight double quotes for UI labels, as G3M does.
- Plurals: two forms (one, other). Write "1 mod" and "2 mods"; the loanword takes -s. Replace the "mod(s)" and "operación(es)" hacks with real plural forms.
- Use neutral Latin American vocabulary shared with VS Code.

### ja

- Address: polite form (です and ます) in sentences, noun-ending form on buttons and menu items ("保存", "適用", "テーマの保存"). G3M uses です or ます 447 times and ください 117 times.
- No spaces between Japanese and Latin letters or digits ("MODを更新", "G3Mの設定"). A few G3M strings have spaces ("MOD を"); remove them on the next pass.
- Colon after a label is an ASCII colon with a space ("言語: {language}"), as in 175 G3M strings. Sentence end is `。`.
- Quotes: 「」 for names and UI labels ("プロファイル「{name}」").
- Keep the katakana long vowel mark: フォルダー, インスペクター, エミッター, ブラウザー. G3M's existing "ビューア" stays as is.
- Counters: "{count} 件" for items, "{count} 個" for physical things. No plural forms.
- Write MOD in capitals, as in 197 G3M strings.

### ko

- Address: polite formal (합니다체). G3M uses -습니다 or -합니다 474 times and -세요 136 times for prompts ("선택하세요"). Labels and buttons are nouns or verb stems ("저장", "적용", "내보내기"). Avoid 해요체.
- Colon after a label: ASCII colon plus space ("언어: {language}"). Quotes: straight single quotes around names ('{name}').
- Counters: "{count}개" for items. No plural forms.
- Particles: avoid "을(를)" and "이(가)" by rewording around a placeholder ("프로필 '{name}' 내보내기에 실패했습니다"). Use the dual form only when rewording is impossible.
- Use 설정 (not 환경 설정) and 폰트 (not 글꼴) per G3M. Loanwords follow the standard transliteration (셰이더, 스프라이트, 에셋).

### zh_cn

- Address: neutral imperative with 请 (104 uses in G3M). 您 appears 14 times in confirmations; use it for confirmation questions only ("您确定要删除此配置吗？"), otherwise omit the pronoun.
- Colon after a label: full-width `：` (267 uses), no space after it. Quotes: curly “ ” for names and UI labels (36 uses). Sentence end is `。`.
- Put a space between Chinese text and Latin letters in product names ("G3M 设置", "DATA 文件"), but not before digits in "第1章". G3M is mixed here; follow this rule.
- Counters: "{count} 个". No plural forms.
- Mainland vocabulary: 文件, 文件夹, 设置, 加载, 导入, 创建, 默认, 保存.

### zh_tw

- Address: same as zh_cn (請 plus imperative, 您 only in confirmations).
- Colon after a label: full-width `：`. Quotes: corner brackets 「」 (35 uses in G3M). Sentence end is `。`.
- Spacing around Latin letters as in zh_cn.
- Counters: "{count} 個". No plural forms.
- Use Taiwan vocabulary, not converted mainland text: 檔案, 資料夾, 設定, 載入, 匯入, 匯出, 建立, 預設, 圖示, 套用, 儲存, 伺服器, 軟體. G3M still has mainland forms to fix on the next pass: 創建 (use 建立), 加載 (use 載入), 導入 (use 匯入), 應用 (use 套用), 圖標 (use 圖示), 設置 (use 設定).
- Keep 補丁 as the noun for patch and 修補 for the patching process, as in G3M.

Added 2026-10-25: Explorer (left resource tree panel), from VS Code language packs: ru Проводник, es Explorador, ja エクスプローラー, ko 탐색기, zh_cn 资源管理器, zh_tw 檔案總管.
