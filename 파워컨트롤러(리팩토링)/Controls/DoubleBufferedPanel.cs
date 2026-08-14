using System.Windows.Forms;

namespace ShowroomPowerController
{
    public class DoubleBufferedPanel : Panel
    {
        public DoubleBufferedPanel()
        {
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            this.UpdateStyles();
        }
    }

    // 4. 규격이 가로 225 x 세로 175로 완전 고정/단일화되고, 하단에 연동 자식 기기들의 상태등을 내포하는 정형 카드 컨트롤
}
