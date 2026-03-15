using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DSDCore
{
    public enum Keyword
    {
        directive,
        def,
        dom,
        parameters,
        interval,   //类型
        distribution,//分布
        variation,  //变异

        sweeps,

        rates,

        plot_settings,
        x_label,
        y_label,
        title,
        label_font_size,
        tick_font_size,
        x_tick,
        y_tick,

        simulation,
        initial,
        final,
        points,
        plots,
        prune,
        multicore,


        simulator,
        sundials,
        stochastic,
        scale,
        seed,
        step,
        trajectories,
        lna,
        cme,
        pde,
        deterministic,
        stiff,

        spatial,
        diffusibles,
        default_diffusion,
        dimensions,
        random,
        nx,
        dt,
        xmax,
        boundary,


        moments,
        order,
        species,
        default_variance,
        initial_mean,
        initial_variance,

        inference,
        name,
        burnin,
        samples,
        thin,
        noise_model,
        timer,
        partial,

        data,

        units,
        time,
        space,
        conceration,


        compilation,
        default_,
        finite,
        infinite,

        unproductive,
        jit,
        leaks,
        declare,
        polymers,
        leak,
        tau,
        migrate,
        lengths,
        tolerance,
        toeholds,

        //colour,
        //bind,
        //unbind,

        tether,

        rendering,
        mode,
        nucleotides,
        new_,
        true_,
        false_,
        adjacent,
        not,
        //seq,
        subdomains,

        locations,
        //后面扩充
    }
    public enum Units
    {
        h,
        min,
        s,
        ms,
        us,
        ns,
        m,
        mm,
        um,
        nm,
        pm,
        fm,
        M,
        mM,
        uM,
        nM,
        pM,
        fM,
        aM,
        zM,
        yM,
    }

    public enum Operator
    {
        Toehold,        // ^
        Comma,          // ,
        Colon,          // :
        TwoColon,       // ::
        Semicolon,      // ;
        Star,           // *
        Slash,          // /
        Plus,           // +
        Minus,          // -
        Equal,          // =
        LeftStrand,     // <
        RightStrand,    // >
        LeftBracket,    // [
        RightBracket,   // ]
        ParentOpen,     // (
        ParentClose,    // )
        LeftBrace,      // {
        RightBrace,     //  }
        Bar,            // |
        At,             // @
        Mod,            // %
        Underline,     // _
    }
    public enum TokenType
    {
        Integer,
        Name,
        String,
        Float,
        Char,
        Keyword,
        Operator,
        Units,
        EOF
    }
}
