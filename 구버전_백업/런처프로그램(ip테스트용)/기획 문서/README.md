# Showroom Launcher (시연 콘텐츠 통합 제어 실행기)

전시장, 팝업스토어, 행사장에서 여러 시연용 콘텐츠(게임, 미디어 아트, 웹 등)를 관리자가 편리하게 일괄 제어하고 자동화 기동 및 모니터링하기 위해 C# Windows Forms로 설계된 고해상도 경량형 런처 솔루션입니다.

---

## 📁 1. 프로젝트 폴더 구성 및 컴파일 방법

프로젝트의 개발 환경 및 컴파일 구조는 외부 리소스 의존성을 배제하여 압축 해제 후 곧바로 동작하는 무설치 컴파일 사양으로 설계되었습니다.

### 주요 파일 목록
* **[Program.cs](file:///c:/Users/user/Documents/VSCode/실행기/Program.cs)**: 단일 코드 파일로 작성된 런처 GUI 및 비즈니스 핵심 로직 파일입니다.
* **[build.bat](file:///c:/Users/user/Documents/VSCode/실행기/build.bat)**: 별도의 무거운 IDE(Visual Studio 등) 없이도 Windows 내장 .NET Framework 컴파일러(`csc.exe`)를 호출해 1초 만에 빌드해 주는 컴파일 스크립트입니다.
* **icon.ico**: 실행 파일(`Launcher.exe`)에 결합될 Win32 아이콘 리소스입니다. (컴파일 단계에서 내부에 영구 삽입됩니다)

### 빌드 및 배포 방법
1. [build.bat](file:///c:/Users/user/Documents/VSCode/실행기/build.bat) 파일을 더블 클릭하여 실행합니다.
2. 컴파일이 성공하면 동일 폴더에 **`Launcher.exe`** 파일이 생성됩니다.
3. 타 PC에 배포할 때는 오직 **`Launcher.exe`** 파일 하나만 복사해서 전달하면 끝납니다. (설정 및 로그 텍스트 파일은 런타임에 런처 본체가 자율 자동 생성합니다)

---

## 🔍 2. 큰 기능 범주별 기술 사양 분석서

런처의 세부 설계 사양은 기능적 연관성에 맞춰 아래의 세부 분석 문서로 각각 상세히 분할 및 기술되어 있습니다. 확인을 원하시는 사양서를 클릭해 상세 구성을 살펴보실 수 있습니다.

### 🎨 [Part 1] GUI 디자인 시스템 및 DPI 스케일링 대응 사양
* **[Feature_UI_Design.md](file:///c:/Users/user/Documents/VSCode/실행기/Feature_UI_Design.md)**
* 프리미엄 다크 모드 컬러 코드 사양, GDI+ 안티앨리어싱 둥근 테두리 렌더링, DoubleBufferedPanel 플리커 프리 기법, 150%/200% High DPI 자동 배율 레이아웃 마진 대응 구조, 실시간 아이콘/이모지 미리보기 기능.

### ⚙️ [Part 2] 지능형 포커스 제어 및 프로세스 생사 감시 체계
* **[Feature_Process_Control.md](file:///c:/Users/user/Documents/VSCode/실행기/Feature_Process_Control.md)**
* 래퍼/컨테이너 빌드 대응 메인 시연 콘텐츠 지연 기동 구조, 10초 미만 기동 지연 보호 밸리데이션 검사, 런처 활성화 상태에 감응하는 상호작용형 동적 Topmost 및 포커스 복원 체계, 권한 예외를 방어하는 이름 검색 기반 더블 체크 종단 감시(Watchdog) 시스템.

### ⏰ [Part 3] 시스템 트레이, Mutex 중복 차단 및 자동 종료 스케줄러
* **[Feature_Tray_Scheduler.md](file:///c:/Users/user/Documents/VSCode/실행기/Feature_Tray_Scheduler.md)**
* 런처 포커스 상태에서의 ESC 트레이 강제 최소화 이동 구조, 단일 인스턴스 동작을 강제하는 Mutex 체계, 윈도우 메시지(`WM_SHOWME`) 기반 기존 인스턴스 복원 통신 로직, 스케줄러 기반 프로세스 순차 종료 및 시스템 전원(PC 끄기) 제어, BaseDirectory 귀속형 `config.json` 절대 경로 고정 시스템.
