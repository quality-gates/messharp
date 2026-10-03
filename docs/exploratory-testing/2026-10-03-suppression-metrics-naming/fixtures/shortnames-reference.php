<?php
class ShortNames {
    public function locals($items, $obj) {
        $aa = 1;
        foreach ($items as $b) { $aa += $b; }
        for ($c = 0; $c < 2; $c++) { $aa += $c; }
        try { $aa++; } catch (Exception $h) { echo $h; }
        return $aa;
    }
    public function lambdas($items) {
        $sum = array_filter($items, fn($j) => $j > 0);
        $add = function ($k, $l) { return $k + $l; };
        return $sum + $add(1, 2);
    }
}
