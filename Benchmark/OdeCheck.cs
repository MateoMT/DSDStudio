using ODE;

namespace Benchmark;

public sealed class OdeCheck
{
    public int EquationCount { get; private set; }
    public int NameCount { get; private set; }
    public int EmptyEquationCount { get; private set; }
    public int TotalTermCount { get; private set; }
    public int ZeroCoefficientCount { get; private set; }
    public int NonFiniteCoefficientCount { get; private set; }
    public int InvalidFactorCount { get; private set; }
    public int TotalFactorCount { get; private set; }
    public int MaxTermsPerEquation { get; private set; }
    public int MaxFactorsPerTerm { get; private set; }
    public int MaxExponent { get; private set; }
    public double MaxAbsCoefficient { get; private set; }
    public double? InitialMaxAbsDerivative { get; private set; }
    public int InitialNonFiniteDerivativeCount { get; private set; }
    public List<string> Warnings { get; } = new();

    public static OdeCheck Check(ODEsys odes, Dictionary<int, double> init)
    {
        var check = new OdeCheck();
        check.EquationCount = odes.equations.Count;
        check.NameCount = odes.names.Count;
        check.EmptyEquationCount = odes.equations.Values.Count(e => e.terms.Count == 0);
        check.TotalTermCount = odes.equations.Values.Sum(e => e.terms.Count);
        check.MaxTermsPerEquation = odes.equations.Values.Count == 0 ? 0 : odes.equations.Values.Max(e => e.terms.Count);

        int zeroCoefficientCount = 0;
        int nonFiniteCoefficientCount = 0;
        int invalidFactorCount = 0;
        int totalFactorCount = 0;
        int maxFactorsPerTerm = 0;
        int maxExponent = 0;
        double maxAbsCoefficient = 0;

        foreach (ODEequation equation in odes.equations.Values)
        {
            foreach (ODEterm term in equation.terms)
            {
                if (term.k == 0)
                    zeroCoefficientCount++;
                if (!double.IsFinite(term.k))
                    nonFiniteCoefficientCount++;

                maxAbsCoefficient = Math.Max(maxAbsCoefficient, Math.Abs(term.k));
                maxFactorsPerTerm = Math.Max(maxFactorsPerTerm, term.factors.Count);
                totalFactorCount += term.factors.Count;

                foreach ((int id, int exponent) in term.factors)
                {
                    maxExponent = Math.Max(maxExponent, exponent);
                    if (exponent <= 0 || !odes.equations.ContainsKey(id))
                        invalidFactorCount++;
                }
            }
        }

        check.ZeroCoefficientCount = zeroCoefficientCount;
        check.NonFiniteCoefficientCount = nonFiniteCoefficientCount;
        check.InvalidFactorCount = invalidFactorCount;
        check.TotalFactorCount = totalFactorCount;
        check.MaxFactorsPerTerm = maxFactorsPerTerm;
        check.MaxExponent = maxExponent;
        check.MaxAbsCoefficient = maxAbsCoefficient;

        AddDerivativeStats(check, odes, init);
        AddWarnings(check);
        return check;
    }

    public void Print()
    {
        Console.WriteLine(
            $"ODE check: equations={EquationCount}, names={NameCount}, terms={TotalTermCount}, emptyEquations={EmptyEquationCount}, zeroCoeffTerms={ZeroCoefficientCount}");
        Console.WriteLine(
            $"ODE check: factors={TotalFactorCount}, maxTermsPerEquation={MaxTermsPerEquation}, maxFactorsPerTerm={MaxFactorsPerTerm}, maxExponent={MaxExponent}, maxAbsCoeff={MaxAbsCoefficient:E3}");

        if (InitialMaxAbsDerivative.HasValue)
            Console.WriteLine($"ODE check: initialMaxAbsDerivative={InitialMaxAbsDerivative.Value:E3}, nonFiniteInitialDerivatives={InitialNonFiniteDerivativeCount}");

        foreach (string warning in Warnings)
            Console.WriteLine($"ODE warning: {warning}");
    }

    private static void AddDerivativeStats(OdeCheck check, ODEsys odes, Dictionary<int, double> init)
    {
        try
        {
            double[] y = odes.equations.Keys.Select(id => init.TryGetValue(id, out double value) ? value : 0.0).ToArray();
            double[] dydt = odes.GetRightHandSide()(0, y);

            int nonFiniteCount = 0;
            double maxAbs = 0;
            foreach (double value in dydt)
            {
                if (!double.IsFinite(value))
                    nonFiniteCount++;
                else
                    maxAbs = Math.Max(maxAbs, Math.Abs(value));
            }

            check.InitialMaxAbsDerivative = maxAbs;
            check.InitialNonFiniteDerivativeCount = nonFiniteCount;
        }
        catch (Exception ex)
        {
            check.Warnings.Add($"Initial derivative evaluation failed: {ex.Message}");
        }
    }

    private static void AddWarnings(OdeCheck check)
    {
        if (check.EquationCount != check.NameCount)
            check.Warnings.Add("Equation count does not match name count.");
        if (check.TotalTermCount == 0)
            check.Warnings.Add("No ODE terms were generated. This can happen for very small substance counts, because generated products are not allowed to overlap reactants.");
        if (check.EmptyEquationCount > 0)
            check.Warnings.Add($"{check.EmptyEquationCount} equations have no terms and will remain constant.");
        if (check.NonFiniteCoefficientCount > 0)
            check.Warnings.Add($"{check.NonFiniteCoefficientCount} non-finite coefficients were found.");
        if (check.InvalidFactorCount > 0)
            check.Warnings.Add($"{check.InvalidFactorCount} invalid factors were found.");
        if (check.MaxExponent >= 3 && check.MaxAbsCoefficient >= 20)
            check.Warnings.Add("High-order terms with large coefficients may make RK45 reduce dt aggressively.");
        if (check.InitialNonFiniteDerivativeCount > 0)
            check.Warnings.Add($"{check.InitialNonFiniteDerivativeCount} initial derivatives are non-finite.");
        if (check.InitialMaxAbsDerivative.HasValue && check.InitialMaxAbsDerivative.Value > 1e6)
            check.Warnings.Add("Initial derivative magnitude is very large; the random system may be stiff or explosive.");
    }
}
