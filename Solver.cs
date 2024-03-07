using TwoCaptcha.Captcha;

namespace MidpassAutoQueue;

public class Solver
{
    private TwoCaptcha.TwoCaptcha _solver;
    public Solver(string token)
    {
        _solver = new(token);
    }

    public async Task<(string code, string id)?> SolveCaptcha(string path)
    {
        Normal captcha = new Normal();
        captcha.SetFile(path);
        captcha.SetNumeric(4);
        captcha.SetMinLen(6);
        captcha.SetMaxLen(6);
        captcha.SetCaseSensitive(true);
        captcha.SetLang("en");
        captcha.SetHintText("First symbol is always small letter, other are numbers");

        try
        {
            await _solver.Solve(captcha);
            return (captcha.Code, captcha.Id);
        }
        catch { }

        return null;
    }

    public async Task Report(string id, bool isCorrect = true) => await _solver.Report(id, isCorrect);
}
